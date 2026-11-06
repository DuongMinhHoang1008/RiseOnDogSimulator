using System;
using UnityEditor;

namespace ReferenceDog.Animation.Editor
{
    public sealed class ReferenceDogRigImporter : AssetPostprocessor
    {
        private bool IsRig => assetPath.StartsWith("Assets/Art/ReferenceDog/", StringComparison.Ordinal)
            && assetPath.EndsWith("Dog_Rigged.fbx", StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsRig) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
            importer.isReadable = false;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsRig) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                var separator = clip.name.LastIndexOf('|');
                if (separator >= 0) clip.name = clip.name.Substring(separator + 1);
                clip.loopTime = clip.name == "Idle" || clip.name == "Run";
                clip.loopPose = clip.loopTime;
                // Root movement is baked into the pose. The controller uses applyRootMotion=false.
                clip.lockRootRotation = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
