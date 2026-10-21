p = r'D:\Dev\deskPet_cachu\tools\StarterExtract.cs'
s = open(p, encoding='utf-8-sig').read()
s = s.replace("if (x >= 380 && x <= 525 && y < 172) return 3;   // Charmander teal eyes", "if (x >= 380 && x <= 500 && y < 172) return 3;   // Charmander teal eyes")
i = s.index("public static class Reconstruct")
s = s[:i]
open(p, 'w', encoding='utf-8-sig').write(s)
p = r'D:\Dev\deskPet_cachu\tools\extract-starters.ps1'
s = open(p, encoding='utf-8-sig').read()
s = s.replace('(Get-Content "$PSScriptRoot\\StarterExtract.cs" -Raw -Encoding UTF8)', '((Get-Content "$PSScriptRoot\\StarterExtract.cs" -Raw -Encoding UTF8) + "`n" + (Get-Content "$PSScriptRoot\\Reconstruct.cs" -Raw -Encoding UTF8))')
open(p, 'w', encoding='utf-8-sig').write(s)
b = open(r'D:\Dev\deskPet_cachu\tools\Reconstruct.cs', encoding='utf-8').read()
open(r'D:\Dev\deskPet_cachu\tools\Reconstruct.cs', 'w', encoding='utf-8-sig').write(b)
print('ok')
