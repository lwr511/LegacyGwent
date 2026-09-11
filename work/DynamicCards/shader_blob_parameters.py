# Unity 2021+ separates program code and binding parameters into different blob entries.
# Layout cross-checked against AssetRipper ShaderSubProgram.ReadParameters at 7b1c20c1f316612d5b009565b722a191acab9183.
import struct
class Reader:
 def __init__(self,data):self.data=data;self.p=0
 def integer(self):v=struct.unpack_from('<i',self.data,self.p)[0];self.p+=4;return v
 def string(self):n=self.integer();assert 0<=n<100000;v=self.data[self.p:self.p+n].decode();self.p=(self.p+n+3)&~3;return v

def parameters(data,passinfo):
 r=Reader(data);assert r.integer()==202012090
 indices={name:index for name,index in passinfo['m_NameIndices']}
 def nameid(name):
  if name not in indices:indices[name]=max(indices.values(),default=-1)+1;passinfo['m_NameIndices'].append([name,indices[name]])
  return indices[name]
 p=dict(m_VectorParams=[],m_MatrixParams=[],m_ConstantBuffers=[],m_ConstantBufferBindings=[],m_TextureParams=[],m_BufferParams=[])
 groups=r.integer();assert 0<=groups<100
 for group in range(groups):
  name=r.string();used=r.integer();count=r.integer();vectors=[];matrices=[]
  for j in range(count):
   pn=r.string();typ=r.integer();rows=r.integer();columns=r.integer();matrix=r.integer();array=r.integer();index=r.integer()
   item=dict(m_NameIndex=nameid(pn),m_Type=typ,m_Index=index,m_ArraySize=array)
   if matrix:item['m_RowCount']=rows;matrices.append(item)
   else:item['m_Dim']=columns;vectors.append(item)
  structs=r.integer()
  if structs:raise ValueError('Structured shader parameters require explicit translation')
  if group==0:p['m_VectorParams']=vectors;p['m_MatrixParams']=matrices
  else:p['m_ConstantBuffers'].append(dict(m_NameIndex=nameid(name),m_Size=used,m_VectorParams=vectors,m_MatrixParams=matrices))
 count=r.integer()
 for j in range(count):
  name=r.string();typ=r.integer();index=r.integer();extra=r.integer();item=dict(m_NameIndex=nameid(name),m_Index=index)
  if typ==0:
   detail=r.integer();item.update(m_SamplerIndex=extra,m_Dim=detail>>1);p['m_TextureParams'].append(item)
  elif typ==1:p['m_ConstantBufferBindings'].append(item)
  elif typ==2:p['m_BufferParams'].append(item)
  elif typ not in [3,4]:raise ValueError('Unknown shader binding '+str(typ))
 return p
