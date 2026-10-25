using System.Globalization;
namespace AskCons;
public static class AnalysisMath
{
    public static double Calculate(string expression)=>new Parser(expression).Run();
    private sealed class Parser(string expression) {
        int at;int depth;
        public double Run(){if(expression.Length>2000)throw new ArgumentException("Expression too long.");var result=Add();Space();if(at!=expression.Length||!double.IsFinite(result))throw new ArgumentException("Invalid or non-finite expression.");return result;}
        void Space(){while(at<expression.Length&&char.IsWhiteSpace(expression[at]))at++;}
        bool Take(char c){Space();if(at<expression.Length&&expression[at]==c){at++;return true;}return false;}
        double Add(){var n=Multiply();while(true){if(Take('+'))n+=Multiply();else if(Take('-'))n-=Multiply();else return n;}}
        double Multiply(){var n=Unary();while(true){if(Take('*'))n*=Unary();else if(Take('/'))n/=Unary();else if(Take('%'))n%=Unary();else return n;}}
        double Unary(){if(++depth>64)throw new ArgumentException("Expression too deep.");try{if(Take('+'))return Unary();if(Take('-'))return -Unary();return Power();}finally{depth--;}}
        double Power(){var n=Atom();return Take('^')?Math.Pow(n,Unary()):n;}
        double Atom(){
            if(++depth>64)throw new ArgumentException("Expression too deep.");
            try {

                if(Take('(')){var n=Add();if(!Take(')'))throw new ArgumentException("Missing closing parenthesis.");return n;}
                Space();int start=at;
                if(at<expression.Length&&char.IsLetter(expression[at])){
                    while(at<expression.Length&&char.IsLetter(expression[at]))at++;
                    var name=expression[start..at].ToLowerInvariant();
                    if(name=="pi")return Math.PI;if(name=="e")return Math.E;
                    if(!Take('('))throw new ArgumentException("Function needs parentheses.");
                    var args=new List<double>{Add()};while(Take(','))args.Add(Add());if(!Take(')'))throw new ArgumentException("Missing closing parenthesis.");
                    double One(Func<double,double> f)=>args.Count==1?f(args[0]):throw new ArgumentException("Function needs one argument.");
                    return name switch {
                        "abs"=>One(Math.Abs),"sqrt"=>One(Math.Sqrt),"log"=>One(Math.Log10),"ln"=>One(Math.Log),"exp"=>One(Math.Exp),
                        "round"=>args.Count==1?Math.Round(args[0]):args.Count==2?Math.Round(args[0],checked((int)args[1])):throw new ArgumentException("round(value, digits)"),
                        "pow"=>args.Count==2?Math.Pow(args[0],args[1]):throw new ArgumentException("pow needs two arguments."),
                        "sum"=>args.Sum(),"avg"=>args.Average(),"min"=>args.Min(),"max"=>args.Max(),
                        "stddev"=>Math.Sqrt(args.Select(x=>Math.Pow(x-args.Average(),2)).Average()),
                        _=>throw new ArgumentException("Unknown arithmetic function.")
                    };
                }
                while(at<expression.Length&&(char.IsDigit(expression[at])||expression[at]=='.'))at++;
                if(at<expression.Length&&(expression[at]=='e'||expression[at]=='E')){
                    at++;if(at<expression.Length&&(expression[at]=='+'||expression[at]=='-'))at++;
                    while(at<expression.Length&&char.IsDigit(expression[at]))at++;
                }
                if(start==at)throw new ArgumentException("Expected a number.");
                return double.Parse(expression[start..at],CultureInfo.InvariantCulture);
            }finally{depth--;}
        }
    }
    public static EstimateResult Estimate(QueryData data,string method,DateTime target,double? annualRate=null) {
        if(data.Truncated)throw new ArgumentException("Estimation needs a complete series. Aggregate or narrow the query before estimating.");
        bool missing=data.Rows.Any(row=>row.Length<2||row[0]==null||row[1]==null);
        var points=data.Rows.Where(row=>row.Length>=2&&row[0]!=null&&row[1]!=null).Select(row=>new TimePoint(DateTime.Parse(Convert.ToString(row[0],CultureInfo.InvariantCulture)!,CultureInfo.InvariantCulture),
            row[1]==null?throw new ArgumentException("A series value is missing. Filter missing points explicitly."):Convert.ToDouble(row[1],CultureInfo.InvariantCulture))).OrderBy(x=>x.Date).ToList();
        if(points.Count==0)throw new ArgumentException("No baseline is available. Query a recorded baseline first.");
        if(points.Any(x=>!double.IsFinite(x.Value)))throw new ArgumentException("Non-finite series value.");
        bool conflicts=points.GroupBy(x=>x.Date).Any(g=>g.Select(x=>x.Value).Distinct().Count()>1);
        string inputNote=(missing?" Missing observations omitted; no zero values invented.":"")+
            (conflicts?" WARNING: conflicting values on the same date were averaged for this estimate; this average is not an authoritative recorded valuation. Prefer AskLatestSnapshots to select the latest capture.":"");
        if(conflicts)points=points.GroupBy(x=>x.Date).Select(g=>new TimePoint(g.Key,g.Average(x=>x.Value))).OrderBy(x=>x.Date).ToList();
        points=points.DistinctBy(x=>x.Date).ToList();
        var exact=points.FirstOrDefault(x=>x.Date==target);
        if(exact!=null && method=="linear"&&!conflicts)return new(target,exact.Value,"supplied point","none","Exact point in the supplied query result; no interpolation by this tool. Recording provenance depends on the source query.",new[]{exact});
        bool outside=target<points[0].Date||target>points[^1].Date;
        string kind=outside?"EXTRAPOLATED ESTIMATE":"INTERPOLATED / FITTED ESTIMATE";
        double value;string assumptions;List<TimePoint> evidence;
        if(method=="compound" && annualRate.HasValue){
            if(annualRate<=-1||!double.IsFinite(annualRate.Value))throw new ArgumentException("Annual growth assumption must be finite and greater than -100%.");
            var baseline=points.LastOrDefault(x=>x.Date<=target)??points[0];
            value=baseline.Value*Math.Pow(1+annualRate.Value,(target-baseline.Date).TotalDays/365.25);
            assumptions=$"Assumed annual growth rate {annualRate.Value:P4}; baseline {baseline.Date:yyyy-MM-dd}, target {target:yyyy-MM-dd}, horizon {(target-baseline.Date).TotalDays:N2} days; constant compounding, no future deposits/withdrawals, 365.25-day year. This rate is a scenario assumption, not a recorded fact.";
            evidence=new(){baseline};kind="SCENARIO / "+kind;
        } else {
            if(points.Count<2)return new(target,points[0].Value,"constant carry-forward","ASSUMED ESTIMATE",
                "Only one distinct dated observation is available. Constant value assumed; no growth trend can be established."+inputNote,points);
            if(method=="linear_regression"){
                double origin=points[0].Date.Ticks;var xs=points.Select(p=>(p.Date.Ticks-origin)/TimeSpan.TicksPerDay).ToList();
                var mx=xs.Average();var my=points.Average(p=>p.Value);
                var slope=xs.Select((x,i)=>(x-mx)*(points[i].Value-my)).Sum()/xs.Sum(x=>(x-mx)*(x-mx));
                value=my+slope*((target.Ticks-origin)/TimeSpan.TicksPerDay-mx);
                assumptions=$"Ordinary least-squares straight-line fit over {points.Count} observations; past trend continues, no future cash flows or structural changes. No confidence interval is established.";
                evidence=points.Take(2).Concat(points.TakeLast(2)).DistinctBy(x=>x.Date).ToList();
            }else if(method=="linear"||method=="compound"){
                var left=points.LastOrDefault(x=>x.Date<target);
                var right=points.FirstOrDefault(x=>x.Date>target);
                var a=left??points[0];var b=right??points[^1];
                if(left==null)b=points[1];if(right==null)a=points[^2];
                double fraction=(target-a.Date).TotalDays/(b.Date-a.Date).TotalDays;
                if(method=="compound"){
                    if(a.Value<=0||b.Value<=0)throw new ArgumentException("Observed compound growth requires positive baseline values; use linear instead.");
                    value=a.Value*Math.Pow(b.Value/a.Value,fraction);
                    assumptions="Constant compound growth inferred from the two observations; no cash-flow adjustment. This is not an investment-return calculation.";
                }else{
                    value=a.Value+(b.Value-a.Value)*fraction;
                    assumptions=outside?$"Continue the straight-line trend from {a.Date:yyyy-MM-dd} to {b.Date:yyyy-MM-dd} ({(b.Date-a.Date).TotalDays:N2} observed days); target {target:yyyy-MM-dd}, {(target-b.Date).TotalDays:N2} days relative to the later observation. No future cash flows or market changes assumed.":
                        $"Straight-line interpolation between {a.Date:yyyy-MM-dd} and {b.Date:yyyy-MM-dd}; within-period path is unknown.";
                }
                evidence=new(){a,b};
            }else throw new ArgumentException("Use linear, linear_regression or compound.");
        }
        if(!double.IsFinite(value))throw new ArgumentException("The assumptions produce a non-finite estimate. Choose another method or horizon.");
        if(value<0&&points.All(p=>p.Value>=0))assumptions+=" WARNING: this trend crosses below zero; the projection may be economically unrealistic.";
        return new(target,value,method,kind,assumptions+inputNote,evidence);
    }
}
public sealed record EstimateResult(DateTime Date,double Value,string Method,string Warning,string Assumptions,IEnumerable<TimePoint> Evidence);



