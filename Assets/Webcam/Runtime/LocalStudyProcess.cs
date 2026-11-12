using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace CampusCommons.Webcam
{
    // Owns only the process it starts. No global process-name termination.
    public sealed class LocalStudyProcess : IDisposable
    {
        Process process;
        public string Origin {get; private set;}
        public string Token {get; private set;}
        public bool Alive {get {try{return process!=null && !process.HasExited;}catch{return false;}}}
        public static string RuntimeDirectory => Environment.GetEnvironmentVariable("CAMPUS_QEV_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".campuscommons","qev");
        public void Start()
        {
            if(process!=null)throw new InvalidOperationException("Already started");
            string root=RuntimeDirectory;
            string python=Path.Combine(root,Environment.OSVersion.Platform==PlatformID.Win32NT?".venv/Scripts/python.exe":".venv/bin/python");
            string script=Path.Combine(root,"app","live.py");
            if(!File.Exists(python)||!File.Exists(script))throw new FileNotFoundException("로컬 모델 설치가 필요합니다. Tools/webcam 설치 안내를 확인해 주세요.");
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
            Origin="http://127.0.0.1:"+port;Token=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");
            var info=new ProcessStartInfo(python) {UseShellExecute=false,CreateNoWindow=true,
                WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,
                Arguments="-u "+Quote(script)+" --model-dir "+Quote(Path.Combine(root,"model"))+" --cache-dir "+Quote(Path.Combine(root,"hf-cache","hub"))+" --port "+port};
            info.EnvironmentVariables["QEV_SESSION_TOKEN"]=Token;
            info.EnvironmentVariables["QEV_PARENT_PID"]=Process.GetCurrentProcess().Id.ToString();
            info.EnvironmentVariables["PYTORCH_ENABLE_MPS_FALLBACK"]="1";
            info.EnvironmentVariables["HF_HUB_OFFLINE"]="1";
            info.EnvironmentVariables["TRANSFORMERS_OFFLINE"]="1";
            process=new Process {StartInfo=info};
            // Drain both pipes; image payloads are never written to them or to files.
            process.OutputDataReceived+=(s,e)=>{};process.ErrorDataReceived+=(s,e)=>{};
            process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
        }
        static string Quote(string s)=>"\""+s.Replace("\"","\\\"")+"\"";
        public void Dispose()
        {
            if(process==null)return;
            try {if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            process.Dispose();process=null;
        }
    }
}
