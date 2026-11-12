using System;using System.Net.Sockets;using System.Threading;using System.IO;using System.Text;using CampusCommons.Webcam;
class Program {
static string Call(LocalStudyProcess local,string path,byte[] body=null){var u=new Uri(local.Origin);using(var c=new TcpClient()){c.ReceiveTimeout=30000;c.SendTimeout=30000;c.Connect(u.Host,u.Port);using(var stream=c.GetStream()){
string h=(body==null?"GET":"POST")+" "+path+" HTTP/1.0\r\nHost: "+u.Authority+"\r\nOrigin: "+local.Origin+"\r\nX-Session-Token: "+local.Token+"\r\n"+(body==null?"":"Content-Type: image/jpeg\r\nContent-Length: "+body.Length+"\r\n")+"\r\n";
byte[] header=Encoding.ASCII.GetBytes(h);stream.Write(header,0,header.Length);if(body!=null)stream.Write(body,0,body.Length);using(var reader=new StreamReader(stream))return reader.ReadToEnd();}}}
static void Main(){using(var local=new LocalStudyProcess()){local.Start();bool ready=false;for(int i=0;i<90;i++){try{var c=Call(local,"/api/config");if(!c.Contains("fast"))throw new Exception(c);ready=true;break;}catch(SocketException){Thread.Sleep(1000);}}
if(!ready)throw new Exception("Startup timeout");var response=Call(local,"/api/infer",File.ReadAllBytes(Path.Combine(Environment.GetEnvironmentVariable("QEV_SMOKE_FIXTURES") ?? throw new Exception("Set QEV_SMOKE_FIXTURES to public images"),"phone.jpg")));if(!response.Contains("\"state\": \"phone\""))throw new Exception(response);
local.Dispose();if(local.Alive)throw new Exception("Owned child still running");Console.WriteLine("NATIVE_PROCESS_HTTP_AND_CLEANUP_PASS");}}}
