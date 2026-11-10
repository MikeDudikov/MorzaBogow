using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// Materials of the environment built by the editor (MorzaBogow.Editor.EnvironmentArt) and kept in
    /// Assets/Art/Environment/Resources/Environment. Null until they are built; callers fall back to the scene's plain ones.
    /// </summary>
    public static class LandscapeArt
    {
        static Material oliveBark, oliveLeaves;

        public static Material OliveBark => oliveBark != null ? oliveBark : oliveBark = Resources.Load<Material>("Environment/OliveBark");

        public static Material OliveLeaves => oliveLeaves != null ? oliveLeaves : oliveLeaves = Resources.Load<Material>("Environment/OliveLeaves");
    }
}
