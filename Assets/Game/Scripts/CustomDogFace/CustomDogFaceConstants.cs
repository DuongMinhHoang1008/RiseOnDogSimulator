namespace DogSimulator.CustomDogFace
{
    public static class CustomDogFaceConstants
    {
        public const int ProtocolVersion = 1;
        public const int LayoutVersion = 1;
        public const int TextureSize = 512;
        public const int MaximumJpegBytes = 128 * 1024;
        public const int MaximumSourceBytes = 16 * 1024 * 1024;
        public const int MaximumSourceDimension = 8192;
        public const int MaximumSourcePixels = 32 * 1024 * 1024;
        public const int MaximumEditorDimension = 2048;
        public const int MaximumUndoStrokes = 20;
        public const int CustomFaceModelId = 1101;
        public const string StorageKey = "DogSimulator.CustomDogFace.v1";
    }

    public enum DogFaceKind : byte
    {
        Default = 0,
        CustomJpeg = 1
    }
}
