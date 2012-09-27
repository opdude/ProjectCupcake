using UnityEditor;

internal class AnimationPostProcessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (assetPath.Contains("nameofmodel"))
        {
            ModelImporter modelImporter = assetImporter as ModelImporter;
            modelImporter.splitAnimations = true;
            modelImporter.generateAnimations = ModelImporterGenerateAnimations.InRoot;

            // Set the number of animations here
            int numAnimations = 1;
            ModelImporterClipAnimation[] animations = new ModelImporterClipAnimation[numAnimations];

            animations[0] = SetClipAnimation("walk", 0, 24, true);
            // Add your new animation splits here, simply changing the arguments

            // Assign the clips to the model importer to automagically do your splits
            modelImporter.clipAnimations = animations;
        }
    }

    private ModelImporterClipAnimation SetClipAnimation(string name, int firstFrame, int lastFrame, bool loop)
    {
        ModelImporterClipAnimation mica = new ModelImporterClipAnimation();
        mica.name = name;
        mica.firstFrame = firstFrame;
        mica.lastFrame = lastFrame;
        mica.loop = loop;
        return mica;
    }
}