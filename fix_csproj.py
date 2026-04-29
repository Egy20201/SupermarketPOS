import re

path = r'c:\Users\Administrator\source\repos\SupermarketPOS\SupermarketPOS.UI\SupermarketPOS.UI.csproj'
with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

out_lines = []
i = 0
fixed_count = 0
while i < len(lines):
    line = lines[i]
    out_lines.append(line)
    
    if '<Compile Include=' in line and '.xaml.cs"' in line and '/>' not in line:
        # Check next line
        if i + 1 < len(lines):
            next_line = lines[i+1]
            if '<DependentUpon>' in next_line:
                out_lines.append(next_line)
                i += 1
                # Check line after DependentUpon
                if i + 1 < len(lines):
                    next_next_line = lines[i+1]
                    if '</Compile>' not in next_next_line:
                        # Missing </Compile>
                        out_lines.append('    </Compile>\n')
                        fixed_count += 1
            else:
                if '</Compile>' not in next_line:
                    out_lines.append('    </Compile>\n')
                    fixed_count += 1
    i += 1

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(out_lines)
print(f"Done fixing csproj! Inserted {fixed_count} missing </Compile> tags.")
