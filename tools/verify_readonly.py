from pathlib import Path
s=Path('ReadOnlyPortfolio.cs').read_text()
assert 'Mode=SqliteOpenMode.ReadOnly' in s and 'PRAGMA query_only=ON' in s
assert 'consolidation.db' in s
for word in ('CREATE TABLE','DROP TABLE','ALTER TABLE','INSERT INTO','UPDATE Accounts','DELETE FROM'):
 assert word not in s,word
assert not (Path('Services')/'DatabaseService.cs').exists()
print('Read-only source contract verified; no ConsolidationNG code bundled.')
