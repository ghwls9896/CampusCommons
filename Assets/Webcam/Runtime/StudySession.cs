namespace CampusCommons.Webcam
{
    // No Unity dependency: stale asynchronous results cannot revive a stopped session.
    public sealed class StudySession
    {
        float lastFrame;
        public void FrameSeen(float now){lastFrame=now;}
        public bool CameraStalled(float now)=>Running && now-lastFrame>5;
        public bool Running { get; private set; }
        public int Epoch { get; private set; }
        public string Label { get; private set; } = "시작을 눌러 주세요";
        public bool Begin(bool seated, bool focused)
        {
            if (!seated || !focused || Running) return false;
            Epoch++; Running=true; Label="준비 중"; return true;
        }
        public void Observe(bool seated,bool focused) { if (!seated) Stop(); }
        public void Stop() { if(Running) Epoch++; Running=false; Label="시작을 눌러 주세요"; }
        public bool Apply(int epoch,string state)
        {
            var label=LabelFor(state);
            if(!Running || Epoch!=epoch || label==null)return false;
            Label=label;return true;
        }
        public static string LabelFor(string state)
        {
            switch(state) {
                case "studying":return "공부중";
                case "drowsy":return "자는중";
                case "away":return "자리비움";
                case "phone":return "휴대폰 사용중";
                case "other":return "다른활동중";
                default:return null;
            }
        }
    }
}
