#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace DogSimulator.CustomDogFace
{
    internal static class DogFaceWindowsPicker
    {
        private const int PathCapacity = 32768;
        private const uint HideReadOnly = 0x00000004;
        private const uint NoChangeDirectory = 0x00000008;
        private const uint FileMustExist = 0x00001000;

        internal static string Open(out uint error)
        {
            // The filter is a double-NUL-terminated list of label and pattern pairs.
            var filter = Marshal.StringToHGlobalUni("PNG or JPEG images\0*.png;*.jpg;*.jpeg\0");
            var file = IntPtr.Zero;
            var title = IntPtr.Zero;
            try
            {
                file = Marshal.AllocHGlobal(PathCapacity * sizeof(char));
                Marshal.WriteInt16(file, 0);
                title = Marshal.StringToHGlobalUni("Choose dog face");

                var dialog = new OpenFileName
                {
                    Size = (uint)Marshal.SizeOf<OpenFileName>(),
                    Owner = GetActiveWindow(),
                    Filter = filter,
                    FilterIndex = 1,
                    File = file,
                    MaxFile = PathCapacity,
                    Title = title,
                    Flags = HideReadOnly | NoChangeDirectory | FileMustExist
                };

                if (GetOpenFileNameW(ref dialog))
                {
                    error = 0;
                    return Marshal.PtrToStringUni(file);
                }

                // The dialog also returns false when the user cancels, with an error code of zero.
                error = CommDlgExtendedError();
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(title);
                Marshal.FreeHGlobal(file);
                Marshal.FreeHGlobal(filter);
            }
        }

        // OPENFILENAMEW uses pointer-sized fields and 32-bit DWORDs.
        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileName
        {
            public uint Size;
            public IntPtr Owner;
            public IntPtr Instance;
            public IntPtr Filter;
            public IntPtr CustomFilter;
            public uint MaxCustomFilter;
            public uint FilterIndex;
            public IntPtr File;
            public uint MaxFile;
            public IntPtr FileTitle;
            public uint MaxFileTitle;
            public IntPtr InitialDirectory;
            public IntPtr Title;
            public uint Flags;
            public ushort FileOffset;
            public ushort FileExtension;
            public IntPtr DefaultExtension;
            public IntPtr CustomData;
            public IntPtr Hook;
            public IntPtr TemplateName;
            public IntPtr Reserved;
            public uint ReservedFlags;
            public uint ExtendedFlags;
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetActiveWindow();

        [DllImport("comdlg32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileNameW(ref OpenFileName dialog);

        [DllImport("comdlg32.dll", ExactSpelling = true)]
        private static extern uint CommDlgExtendedError();
    }
}
#endif
