from pathlib import Path
import re
import xml.etree.ElementTree as ET
xaml = Path("MainWindow.xaml").read_text(encoding="utf-8")
ET.fromstring(xaml)
assert 'Icon="AskCons.ico"' not in xaml, "ICO is not a WPF resource; keep it as ApplicationIcon only"
assert Path("AskCons.ico").is_file()
assert Path("AskCons.png").is_file()
csproj=Path("AskCons.csproj").read_text()
assert '<Resource Include="AskCons.png" />' in csproj
assert '<ApplicationIcon>AskCons.ico</ApplicationIcon>' in csproj
print("WPF window resources and XML validated")

assert 'Test AI connection' in xaml
print("AskCons 0.1.3 AI diagnostic UI verified")
