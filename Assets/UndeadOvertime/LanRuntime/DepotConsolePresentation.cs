using System;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>One selected original cabinet, not a download or a procedural replacement.</summary>
    public sealed class DepotConsolePresentation : MonoBehaviour
    {
        public const string ResourceRoot = "UndeadOvertime/DepotConsole/";
        public const float UniformScale = .65f;
        public static Vector3 Origin => new Vector3(-9, 0, -5.6f);
        // LOD0/1 measured Unity-axis world bounds after uniform scaling. Same tight box in headless/rendered modes.
        public static Vector3 CollisionCenter => new Vector3(-9, .5900375f, -5.6039f);
        public static Vector3 CollisionSize => new Vector3(.741f, 1.180075f, .4238f);
        private Material ownedMaterial;
        public static DepotConsolePresentation Create(Transform parent, Material pbrTemplate)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (!parent.gameObject.activeInHierarchy) throw new InvalidOperationException("Cabinet ownership must attach under an active parent.");
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Cabinet visuals require rendered Play Mode.");
            if (pbrTemplate == null) throw new ArgumentNullException(nameof(pbrTemplate));
            if (parent.position.sqrMagnitude > .000001f || Quaternion.Angle(parent.rotation, Quaternion.identity) > .001f ||
                (parent.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new ArgumentException("Cabinet requires an identity world-space parent.", nameof(parent));
            var root = new GameObject("Original depot power cabinet"); root.transform.SetParent(parent, false);
            root.transform.localPosition = Origin; root.transform.localScale = Vector3.one * UniformScale;
            var result = root.AddComponent<DepotConsolePresentation>();
            try
            {
                // Clone only the already-anchored Lit/PBR variant. Never change or save the accepted template asset.
                if (pbrTemplate.shader == null || pbrTemplate.shader.name != "Universal Render Pipeline/Lit" ||
                    !pbrTemplate.IsKeywordEnabled("_NORMALMAP") || !pbrTemplate.IsKeywordEnabled("_METALLICSPECGLOSSMAP"))
                    throw new InvalidOperationException("Cabinet requires the accepted Lit/PBR template and enabled variants.");
                result.ownedMaterial = new Material(pbrTemplate) { name = "Original cabinet runtime PBR" };
                result.ownedMaterial.SetTexture("_BaseMap", Texture("cabinet_basecolor"));
                result.ownedMaterial.SetTexture("_BumpMap", Texture("cabinet_normal_opengl"));
                result.ownedMaterial.SetTexture("_MetallicGlossMap", Texture("cabinet_metallic_smoothness"));
                result.ownedMaterial.SetColor("_BaseColor", Color.white); result.ownedMaterial.SetFloat("_BumpScale", 1);
                result.ownedMaterial.SetFloat("_Smoothness", 1); result.ownedMaterial.SetFloat("_SmoothnessTextureChannel", 0);
                result.ownedMaterial.SetFloat("_Metallic", 1); result.ownedMaterial.SetFloat("_WorkflowMode", 1);
                result.ownedMaterial.SetFloat("_Surface", 0); result.ownedMaterial.SetFloat("_AlphaClip", 0);
                result.ownedMaterial.SetTexture("_OcclusionMap", null);
                foreach (string keyword in new[] { "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_ALPHATEST_ON", "_EMISSION", "_SPECULAR_SETUP", "_OCCLUSIONMAP", "_SURFACE_TYPE_TRANSPARENT" })
                    result.ownedMaterial.DisableKeyword(keyword);
                foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                { result.ownedMaterial.SetTextureScale(property, Vector2.one); result.ownedMaterial.SetTextureOffset(property, Vector2.zero); }
                var lods = new LOD[3]; var proxy = new Bounds(CollisionCenter, CollisionSize);
                for (int i = 0; i < lods.Length; i++)
                {
                    var model = Resources.Load<GameObject>(ResourceRoot + "UO_DepotConsole_LOD" + i);
                    if (model == null) throw new InvalidOperationException("Missing original cabinet LOD" + i + ".");
                    var instance = Instantiate(model, root.transform, false); instance.name = "Original cabinet LOD" + i;
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1 ||
                        instance.GetComponentsInChildren<Collider>(true).Length != 0 || instance.GetComponentsInChildren<Animation>(true).Length != 0 ||
                        instance.GetComponentsInChildren<LODGroup>(true).Length != 0)
                        throw new InvalidOperationException("Cabinet import must have one single-slot renderer and no collider, animation or LODGroup.");
                    Bounds b = renderers[0].bounds;
                    if (b.min.x < proxy.min.x - .001f || b.min.y < proxy.min.y - .001f || b.min.z < proxy.min.z - .001f ||
                        b.max.x > proxy.max.x + .001f || b.max.y > proxy.max.y + .001f || b.max.z > proxy.max.z + .001f ||
                        b.size.x < .7f || b.size.y < 1.1f || b.size.z < .38f)
                        throw new InvalidOperationException("Cabinet import axes or scale do not match the collision contract.");
                    renderers[0].sharedMaterial = result.ownedMaterial;
                    lods[i] = new LOD(i == 0 ? .18f : i == 1 ? .06f : 0, renderers);
                }
                var group = root.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None;
                group.SetLODs(lods); group.RecalculateBounds();
                return result;
            }
            catch { root.SetActive(false); Destroy(root); throw; }
        }
        private static Texture2D Texture(string name)
        {
            var texture = Resources.Load<Texture2D>(ResourceRoot + name);
            if (texture == null || texture.width != 1024 || texture.height != 1024)
                throw new InvalidOperationException("Missing or incorrectly sized original cabinet texture " + name + ".");
            return texture;
        }
        private void OnDestroy() { if (ownedMaterial != null) Destroy(ownedMaterial); ownedMaterial = null; }
    }
}
