# Regenerates src/TKCode.Arc.Themes.Pixel/Themes/*.xml (compiled theme definitions) from the token tables in ArcPixelTheme.cs.
# The ArcDesign radii are read from the Corners package's CornerTable.g.cs. Control overrides stay C#-only: a compiled theme may not name controls of another assembly.
import re,os
root=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "src", "TKCode.Arc.Themes.Pixel") + "/"
src=open(root+"ArcPixelTheme.cs").read()
def block(name):
    m=re.search(name+r" =\s*\[(.*?)\n    \];",src,re.S); return m.group(1)
font=re.search(r'PixelFontStack = "([^"]*)"',src).group(1)
display="fonts:ArcPixel#Silkscreen, "+font
def toks(b):
    out=[]
    for a,b2,v,f in re.findall(r'\((?:nameof\(ArcDesignTokenKind\.(\w+)\)|"(\w+)"), (?:"([^"]*)"|(PixelFontStack|PixelDisplayFontStack))\)',b):
        out.append((a or b2, v if v else (display if f=="PixelDisplayFontStack" else font)))
    return out
def ctl(name):
    b=block(name); res=[]
    for m in re.finditer(r'\("(\w+)",\s*\[(.*?)\]\)',b,re.S):
        res.append((m.group(1),re.findall(r'\("(\w+)", "([^"]*)"\)',m.group(2))))
    return res
def xml(id,name,app,dark,toks_,ctls):
    algs='<Algorithm Id="Default" />'+('<Algorithm Id="Dark" />' if dark else '')
    s=f'<?xml version="1.0" encoding="UTF-8"?>\n<Theme xmlns="https://atomui.net/schemas/theme/v1" Id="{id}" Name="{name}" Appearance="{app}" IsDefault="false">\n  <Algorithms>\n    {algs}\n  </Algorithms>\n  <Controls>\n    <Control Catalog="TKCode.Arc" Id="ArcDesign">\n      <Tokens>\n'
    for k,v in toks_: s+=f'        <Token Name="{k}" Value="{v}" />\n'
    s+='      </Tokens>\n    </Control>\n'
    for cid,tt in ctls:
        s+=f'    <Control Catalog="TKCode.Arc" Id="{cid}">\n      <Tokens>\n'
        for k,v in tt: s+=f'        <Token Name="{k}" Value="{v}" />\n'
        s+='      </Tokens>\n    </Control>\n'
    return s+'  </Controls>\n</Theme>\n'
# The ArcDesign radii (all zero in the pixel theme) come from the Corners package table.
corners=open(os.path.join(root,"..","TKCode.Arc.Themes.Corners","CornerTable.g.cs")).read()
radii=[(t,"0") for t in re.findall(r'new\("ArcDesign", "(\w+)"',corners)]
shared=radii+toks(block("Shared"))
os.makedirs(root+"Themes",exist_ok=True)
open(root+"Themes/ArcPixel.xml","w").write(xml("ArcPixel","Arc Pixel","Light",False,shared+toks(block("Light")),[]))
open(root+"Themes/ArcPixelDark.xml","w").write(xml("ArcPixelDark","Arc Pixel Dark","Dark",True,shared+toks(block("Dark")),[]))
