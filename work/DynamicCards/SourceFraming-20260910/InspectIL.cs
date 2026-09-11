using System;
using System.Linq;
using Mono.Cecil;
class InspectIL {
 static void Main(string[] a) {var asm=AssemblyDefinition.ReadAssembly(a[0]);foreach(var t in asm.MainModule.Types){if(!System.Text.RegularExpressions.Regex.IsMatch(t.FullName,a[1],System.Text.RegularExpressions.RegexOptions.IgnoreCase))continue;Console.WriteLine("TYPE "+t.FullName);foreach(var f in t.Fields)Console.WriteLine("FIELD "+f.FieldType+" "+f.Name+(f.HasConstant?" = "+f.Constant:""));foreach(var m in t.Methods){Console.WriteLine("METHOD "+m.FullName);if(a.Length>2 && m.HasBody && System.Text.RegularExpressions.Regex.IsMatch(m.Name,a[2],System.Text.RegularExpressions.RegexOptions.IgnoreCase))foreach(var i in m.Body.Instructions)Console.WriteLine(i);}} }
}
