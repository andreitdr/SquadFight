using System.IO;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Renders all UI canvases under a root into a PNG at a given resolution (temporarily switching them to
    /// Screen Space - Camera). Used by QA tests to produce screenshots for different aspect ratios in batch mode.
    /// </summary>
    public static class UICapture
    {
        public static bool Capture(Transform uiRoot, int width, int height, string path, Camera worldCamera = null)
        {
            var canvases = uiRoot.GetComponentsInChildren<Canvas>(true);
            var camGo = new GameObject("[UICaptureCamera]");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.cullingMask = ~0;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previousModes = new RenderMode[canvases.Length];
            var previousCams = new Camera[canvases.Length];
            var previousDistance = new float[canvases.Length];
            try
            {
                cam.targetTexture = rt;
                for (var i = 0; i < canvases.Length; i++)
                {
                    previousModes[i] = canvases[i].renderMode;
                    previousCams[i] = canvases[i].worldCamera;
                    previousDistance[i] = canvases[i].planeDistance;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = cam;
                    canvases[i].planeDistance = 10f - canvases[i].sortingOrder * 0.01f;
                }
                Canvas.ForceUpdateCanvases();
                if (worldCamera != null)
                {
                    var old = worldCamera.targetTexture;
                    worldCamera.targetTexture = rt;
                    worldCamera.Render();
                    worldCamera.targetTexture = old;
                    cam.clearFlags = CameraClearFlags.Depth;
                }
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                return true;
            }
            finally
            {
                for (var i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = previousModes[i];
                    canvases[i].worldCamera = previousCams[i];
                    canvases[i].planeDistance = previousDistance[i];
                }
                cam.targetTexture = null;
                Object.Destroy(camGo);
                rt.Release();
                Object.Destroy(rt);
            }
        }
    }
}
