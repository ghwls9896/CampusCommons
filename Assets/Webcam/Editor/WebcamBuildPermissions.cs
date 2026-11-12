using System.IO;
using System.Xml;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace CampusCommons.Webcam.Editor
{
    public sealed class WebcamBuildPermissions : IPostprocessBuildWithReport
    {
        public int callbackOrder=>100;
        public void OnPostprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.StandaloneOSX)return;
            string path=Path.Combine(report.summary.outputPath,"Contents","Info.plist");
            var xml=new XmlDocument();xml.XmlResolver=null;xml.Load(path);
            var dict=xml.SelectSingleNode("/plist/dict");
            var key=dict.SelectSingleNode("key[.='NSCameraUsageDescription']");
            if(key==null){key=xml.CreateElement("key");key.InnerText="NSCameraUsageDescription";dict.AppendChild(key);dict.AppendChild(xml.CreateElement("string"));}
            key.NextSibling.InnerText="좌석에 앉으면 미리보기를 켜고, 시작을 누르면 이 기기에서만 공부 상태를 판별합니다. 영상은 저장하거나 외부로 전송하지 않습니다.";
            // XmlDocument.Save emits an empty DTD subset ([]) that macOS plutil rejects.
            // Write a canonical Apple plist declaration and UTF-8 without a BOM.
            using(var writer=XmlWriter.Create(path,new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=true}))
            {
                writer.WriteStartDocument();
                writer.WriteDocType("plist","-//Apple//DTD PLIST 1.0//EN","http://www.apple.com/DTDs/PropertyList-1.0.dtd",null);
                xml.DocumentElement.WriteTo(writer);writer.WriteEndDocument();
            }
        }
    }
}
