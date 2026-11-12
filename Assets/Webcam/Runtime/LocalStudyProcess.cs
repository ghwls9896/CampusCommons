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
        FileStream usage;
        public string Origin {get; private set;}
        public string Token {get; private set;}
        public bool Alive {get {try{return process!=null && !process.HasExited;}catch{return false;}}}
        public static string RuntimeDirectory => StudyRuntimeBootstrap.Root;
        public void Start()
        {
            if(process!=null)throw new InvalidOperationException("Already started");
            string root=RuntimeDirectory;
            if(!StudyRuntimeBootstrap.Ready)throw new InvalidOperationException("공부 인식을 준비하고 있습니다. 잠시 기다려 주세요.");
            string python=StudyRuntimeBootstrap.PythonPath;
            string script=Path.Combine(root,"app","live.py");
            if(!File.Exists(python)||!File.Exists(script))throw new FileNotFoundException("공부 인식 파일이 없습니다. 다시 준비하기를 눌러 주세요.");
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
            Origin="http://127.0.0.1:"+port;Token=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");
            var info=new ProcessStartInfo(python) {UseShellExecute=false,CreateNoWindow=true,
                WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,
                Arguments="-u "+Quote(script)+" --model-dir "+Quote(Path.Combine(root,"model"))+" --cache-dir "+Quote(Path.Combine(root,"hf-cache","hub"))+" --port "+port};
            info.EnvironmentVariables["QEV_SESSION_TOKEN"]=Token;
            info.EnvironmentVariables["QEV_PARENT_PID"]=Process.GetCurrentProcess().Id.ToString();
            info.EnvironmentVariables.Remove("PYTHONHOME");
            info.EnvironmentVariables.Remove("PYTHONPATH");
            info.EnvironmentVariables["PYTHONNOUSERSITE"]="1";
            info.EnvironmentVariables["PYTHONUTF8"]="1";
            info.EnvironmentVariables["PYTORCH_ENABLE_MPS_FALLBACK"]="1";
            info.EnvironmentVariables["HF_HUB_OFFLINE"]="1";
            info.EnvironmentVariables["TRANSFORMERS_OFFLINE"]="1";
            process=new Process {StartInfo=info};
            // Drain both pipes; image payloads are never written to them or to files.
            process.OutputDataReceived+=(s,e)=>{};process.ErrorDataReceived+=(s,e)=>{};
            try {
                if(StudyRuntimeBootstrap.Packaged) usage=RuntimePackageInstaller.AcquireUsage(root);
                process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
            } catch { Dispose(); throw; }
        }
        static string Quote(string s)=>"\""+s.Replace("\"","\\\"")+"\"";
        public void Dispose()
        {
            if(process==null){usage?.Dispose();usage=null;return;}
            try {if(!process.HasExited){process.Kill();process.WaitForExit(5000);}}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            // Keep the lease until the child actually exits, including a delayed OS termination.
            if(Alive && usage!=null) {
                var remaining=usage;usage=null;
                var child=process;process=null;
                System.Threading.Tasks.Task.Run(()=>{try{child.WaitForExit();}finally{remaining.Dispose();child.Dispose();}});
            } else {process.Dispose();process=null;usage?.Dispose();usage=null;}
        }
    }
}
