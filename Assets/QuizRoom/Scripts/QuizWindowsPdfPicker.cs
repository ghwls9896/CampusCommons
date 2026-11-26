#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CampusCommons.Quiz
{
    // GetOpenFileNameW runs its own message loop. Run it on a dedicated STA
    // thread so Unity can render and renew its online seat while it is open.
    internal sealed class QuizWindowsPdfPicker
    {
        const uint InitDialog = 0x0110, Destroy = 0x0002, Command = 0x0111;
        const int CancelButton = 2, MaxPathChars = 32768;
        const uint Explorer = 0x80000, EnableHook = 0x20, FileMustExist = 0x1000,
            PathMustExist = 0x800, NoChangeDir = 0x8, HideReadOnly = 0x4;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            public int size;
            public IntPtr owner, instance, filter, customFilter;
            public int maxCustomFilter, filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public string initialDirectory, title;
            public uint flags;
            public ushort fileOffset, fileExtension;
            public IntPtr defaultExtension, customData, hook, templateName, reserved;
            public uint reservedFlags, flagsEx;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate UIntPtr DialogHook(IntPtr child, uint message, UIntPtr wParam, IntPtr lParam);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern bool GetOpenFileNameW(ref OpenFileName options);
        [DllImport("comdlg32.dll")]
        static extern uint CommDlgExtendedError();
        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")]
        static extern IntPtr GetParent(IntPtr window);
        [DllImport("user32.dll", ExactSpelling = true)]
        static extern bool PostMessageW(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
        [DllImport("ole32.dll")]
        static extern int OleInitialize(IntPtr reserved);
        [DllImport("ole32.dll")]
        static extern void OleUninitialize();

        static readonly DialogHook hook = OnDialogMessage;
        [ThreadStatic] static QuizWindowsPdfPicker current;
        readonly object sync = new object();
        IntPtr window;

        public static Task<string> OpenAsync(string directory, CancellationToken token)
        {
            // GetActiveWindow is thread-local. Capture the initiating Unity window here.
            IntPtr owner = GetActiveWindow();
            if (owner == IntPtr.Zero) throw new InvalidOperationException("퀴즈 창을 찾을 수 없습니다.");
            var picker = new QuizWindowsPdfPicker();
            var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { result.TrySetResult(picker.Show(owner, directory, token)); }
                catch (OperationCanceledException) { result.TrySetCanceled(); }
                catch (Exception error) { result.TrySetException(error); }
            }) { IsBackground = true, Name = "Quiz PDF dialog" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return result.Task;
        }

        CancellationToken token;

        [AOT.MonoPInvokeCallback(typeof(DialogHook))]
        static UIntPtr OnDialogMessage(IntPtr child, uint message, UIntPtr wParam, IntPtr lParam)
        {
            var picker = current;
            if (picker == null) return UIntPtr.Zero;
            lock (picker.sync)
            {
                if (message == InitDialog)
                {
                    picker.window = GetParent(child);
                    if (picker.token.IsCancellationRequested) picker.PostCancel();
                }
                else if (message == Destroy) picker.window = IntPtr.Zero;
            }
            return UIntPtr.Zero;
        }

        void PostCancel()
        {
            if (window != IntPtr.Zero)
                PostMessageW(window, Command, new UIntPtr(CancelButton), IntPtr.Zero);
        }

        void Cancel()
        {
            lock (sync) PostCancel();
        }

        string Show(IntPtr owner, string directory, CancellationToken cancellationToken)
        {
            token = cancellationToken;
            token.ThrowIfCancellationRequested();
            Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
            IntPtr file = IntPtr.Zero, filter = IntPtr.Zero;
            try
            {
                file = Marshal.AllocHGlobal(MaxPathChars * sizeof(char));
                Marshal.WriteInt16(file, 0);
                filter = Marshal.StringToHGlobalUni("PDF 문서 (*.pdf)\0*.pdf\0");
                var options = new OpenFileName
                {
                    size = Marshal.SizeOf<OpenFileName>(), owner = owner,
                    file = file, maxFile = MaxPathChars, filter = filter, filterIndex = 1,
                    initialDirectory = directory, title = "공부한 PDF 선택",
                    flags = Explorer | EnableHook | FileMustExist | PathMustExist | NoChangeDir | HideReadOnly,
                    hook = Marshal.GetFunctionPointerForDelegate(hook)
                };
                current = this;
                using (token.Register(Cancel))
                {
                    bool selected = GetOpenFileNameW(ref options);
                    uint error = selected ? 0 : CommDlgExtendedError();
                    token.ThrowIfCancellationRequested();
                    if (error != 0) throw new InvalidOperationException("PDF 선택 창 오류 (" + error + ")");
                    return selected ? Marshal.PtrToStringUni(file) : null;
                }
            }
            finally
            {
                current = null;
                Marshal.FreeHGlobal(filter);
                Marshal.FreeHGlobal(file);
                OleUninitialize();
            }
        }
    }
}
#endif
