using UnityEngine;

[CreateAssetMenu(menuName = "PropHunt/Map")]
public class MapDefinition : ScriptableObject
{
    public string displayName;
    [Tooltip("Scene name or build-settings path; must be enabled in Build Profiles.")]
    public string sceneName;
    [TextArea] public string description;
    public Texture2D preview;
    public PropCatalog catalog;
}
