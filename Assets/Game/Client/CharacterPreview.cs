using UnityEngine;

namespace Game.Client
{
    // A picture of the character for the Character screen: the skin's model on a stage far from the game, seen by its own camera
    // that renders into a texture. It only runs while the screen is open, and a skin with no model (a plain capsule) shows nothing.
    public sealed class CharacterPreview
    {
        const int Layer = 31;
        const int Width = 512;
        const int Height = 704;

        readonly GameObject stage;
        readonly Camera camera;
        readonly Spinner spinner;
        GameObject model;

        public CharacterPreview()
        {
            Texture = new RenderTexture(Width, Height, 16) { name = "CharacterPreview" };
            stage = new GameObject("CharacterPreviewStage") { layer = Layer };
            stage.transform.position = new Vector3(4000f, 0f, 4000f);
            var cameraObject = new GameObject("PreviewCamera") { layer = Layer };
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.25f, 3.6f);
            cameraObject.transform.localRotation = Quaternion.Euler(8f, 180f, 0f);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.05f, 0.06f);
            camera.cullingMask = 1 << Layer;
            camera.fieldOfView = 32f;
            camera.targetTexture = Texture;
            camera.enabled = false;
            spinner = stage.AddComponent<Spinner>();
        }

        public RenderTexture Texture { get; }

        public bool HasModel => model != null;

        public bool Running => camera.enabled;

        // Starts or stops drawing; nothing is rendered while the screen is closed.
        public void SetRunning(bool running)
        {
            camera.enabled = running && model != null;
        }

        // Puts the skin's model on the stage, or clears it for a skin without one.
        public void Show(SkinAsset skin)
        {
            if (model != null)
            {
                Remove(model);
                model = null;
            }

            if (skin != null && skin.prefab != null)
            {
                model = Object.Instantiate(skin.prefab, stage.transform);
                model.name = "PreviewBody";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                SetLayer(model.transform);
            }

            spinner.Target = model != null ? model.transform : null;
        }

        public void Dispose()
        {
            Remove(stage);
            if (Texture != null)
            {
                Texture.Release();
                Remove(Texture);
            }
        }

        static void Remove(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        static void SetLayer(Transform root)
        {
            root.gameObject.layer = Layer;
            foreach (Transform child in root)
            {
                SetLayer(child);
            }
        }

        // Turns the model slowly so it can be seen from every side.
        sealed class Spinner : MonoBehaviour
        {
            public Transform Target;

            void Update()
            {
                if (Target != null)
                {
                    Target.Rotate(0f, 28f * Time.unscaledDeltaTime, 0f, Space.World);
                }
            }
        }
    }
}
