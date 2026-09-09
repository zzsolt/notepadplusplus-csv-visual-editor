"""Small C# string-token lexer used by the raw-literal localization audit."""
from dataclasses import dataclass
import re,json
@dataclass
class Token:
    start:int
    end:int
    raw:str
    text:str
    args:list
    interpolated:bool

def decode_literal(text,verbatim=False):
    if verbatim: return text.replace('""','"')
    def replace(m):
        t=m.group(1)
        pairs={'0':'\0','a':'\a','b':'\b','f':'\f','n':'\n','r':'\r','t':'\t','v':'\v','\\':'\\','"':'"',"'":"'"}
        if t in pairs:return pairs[t]
        if t[0] in 'uUx':return chr(int(t[1:],16))
        raise ValueError(t)
    return re.sub(r'\\(u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}|x[0-9A-Fa-f]{1,4}|.)',replace,text)

def parse_string(s,start):
    match=re.match(r'(\$@|@\$|\$|@)?"',s[start:])
    if not match: raise ValueError(s[start:start+20])
    prefix=match.group(1) or ''
    verbatim='@' in prefix; interp='$' in prefix
    i=start+len(match[0]); literal=[]; args=[]
    while i<len(s):
        c=s[i]
        if c=='"':
            if verbatim and s[i:i+2]=='""': literal.append('""');i+=2;continue
            i+=1
            return Token(start,i,s[start:i],decode_literal(''.join(literal),verbatim),args,interp)
        if c=='\\' and not verbatim:
            literal.append(s[i:i+2]);i+=2;continue
        if interp and c=='{' and s[i:i+2]=='{{':literal.append('{{');i+=2;continue
        if interp and c=='}' and s[i:i+2]=='}}':literal.append('}}');i+=2;continue
        if interp and c=='{':
            begin=i+1;i+=1; depth=0; sep=None
            while i<len(s):
                if s[i] in '"' or re.match(r'(?:\$@|@\$|\$|@)"',s[i:]):
                    tok=parse_string(s,i);i=tok.end;continue
                if s[i]=="'":
                    i+=1
                    while s[i]!="'":i+=2 if s[i]=='\\' else 1
                    i+=1;continue
                if s[i] in '([{':depth+=1
                elif s[i] in ')]}':
                    if s[i]=='}' and depth==0:break
                    depth-=1
                elif s[i] in ':,' and depth==0 and sep is None:sep=i
                i+=1
            if i>=len(s):raise ValueError('Unclosed interpolation')
            arg=s[begin:sep or i].strip()
            fmt=s[sep:i] if sep else ''
            literal.append('{'+str(len(args))+fmt+'}')
            args.append(arg);i+=1;continue
        literal.append(c);i+=1
    raise ValueError('Unclosed string')

def tokens(s):
    i=0
    while i<len(s):
        if s[i:i+2]=='//':
            n=s.find('\n',i);i=n if n>=0 else len(s);continue
        if s[i:i+2]=='/*':
            n=s.find('*/',i+2);i=n+2 if n>=0 else len(s);continue
        if s[i]=="'":
            i+=1
            while i<len(s) and s[i]!="'":i+=2 if s[i]=='\\' else 1
            i+=1;continue
        if s[i]=='"' or re.match(r'(?:\$@|@\$|\$|@)"',s[i:]):
            t=parse_string(s,i);yield t;i=t.end;continue
        i+=1
