#if UNITY_EDITOR
// ============================================================================
// SHADOW CITY — Editor/TripoImporter.cs
// The AI asset pipeline: paste your Tripo key, pick a prompt from the curated
// pack (or write your own), generate → poll → download GLB into
// Assets/ShadowCity/Models/. Costs are shown BEFORE each call so the balance
// is never spent by surprise. (Balance currently 0 — window still works for
// browsing prompts; generation buttons disable when balance is 0.)
// ============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ShadowCity.EditorTools
{
    public class TripoImporter : EditorWindow
    {
        string apiKey = "";
        string prompt = "";
        string status = "";
        float faceLimit = 3000;
        string taskId = "";
        double lastPoll;
        int balance = -1;

        // Curated prompt pack — consistent style across all assets
        const string STYLE = ", stylized low-poly game asset, flat colors, neon dusk city style, single object, clean silhouette";
        static readonly (string label, string prompt, int faces)[] Pack =
        {
            ("Player character", "young adult urban explorer character, hooded jacket, standing T-pose" + STYLE, 8000),
            ("Sedan car",   "compact modern sedan car" + STYLE, 4000),
            ("Sports car",  "sleek low sports car with spoiler" + STYLE, 4000),
            ("Taxi",        "yellow city taxi sedan with roof sign" + STYLE, 4000),
            ("Police car",  "black and white police cruiser with light bar" + STYLE, 4000),
            ("Street lamp", "tall curved street lamp post" + STYLE, 800),
            ("Traffic light","traffic light on pole with three lamps" + STYLE, 800),
            ("Dumpster",    "metal garbage dumpster with lid" + STYLE, 800),
            ("Bench",       "wooden park bench with metal legs" + STYLE, 600),
            ("Neon sign",   "rectangular neon shop sign with tubes frame" + STYLE, 600),
            ("Kiosk",       "small street food kiosk stand with awning" + STYLE, 1500),
            ("Tree",        "stylized city tree with round canopy" + STYLE, 900),
            ("Fire hydrant","red fire hydrant" + STYLE, 500),
            ("Trash can",   "public trash can cylinder" + STYLE, 400),
        };

        [MenuItem("Shadow City/Tripo Importer")]
        public static void Open() => GetWindow<TripoImporter>("Tripo Importer");

        void OnGUI()
        {
            GUILayout.Label("Tripo AI → Unity pipeline", EditorStyles.boldLabel);
            apiKey = EditorGUILayout.PasswordField("API Key (tsk_…)", apiKey);

            if (GUILayout.Button("Check balance"))
                EditorCoroutine(CheckBalance());
            if (balance >= 0)
                EditorGUILayout.HelpBox($"Balance: {balance} credits" +
                    (balance == 0 ? " — top up at tripo3d.ai to enable generation." : ""),
                    balance == 0 ? MessageType.Warning : MessageType.Info);

            GUILayout.Space(8);
            GUILayout.Label("Prompt pack (click to load):", EditorStyles.boldLabel);
            int col = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (var (label, p, faces) in Pack)
            {
                if (GUILayout.Button(label, GUILayout.Width(115)))
                { prompt = p; faceLimit = faces; }
                if (++col % 4 == 0)
                { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
            }
            EditorGUILayout.EndHorizontal();

            prompt = EditorGUILayout.TextArea(prompt, GUILayout.Height(52));
            faceLimit = EditorGUILayout.Slider("Face limit", faceLimit, 300, 20000);

            EditorGUI.BeginDisabledGroup(balance <= 0 || string.IsNullOrEmpty(prompt));
            if (GUILayout.Button("Generate (~25–60 credits)"))
                EditorCoroutine(Generate());
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(taskId) && GUILayout.Button("Poll status / download"))
                EditorCoroutine(Poll());

            GUILayout.Space(6);
            EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.HelpBox(
                "Import steps after download:\n" +
                "1) The GLB lands in Assets/ShadowCity/Models/\n" +
                "2) Unity 2022 needs a GLB importer: add 'glTFast' via Package Manager\n" +
                "   (Window→Package Manager→+→Add by name→com.unity.cloud.gltfast)\n" +
                "3) Drag the model onto the matching factory slot or replace the\n" +
                "   procedural visual child of the prefab.", MessageType.Info);
        }

        IEnumerator<object> CheckBalance()
        {
            using var req = UnityWebRequest.Get("https://api.tripo3d.ai/v2/openapi/user/balance");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return req.SendWebRequest();
            while (!req.isDone) yield return null;
            if (req.responseCode == 200)
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    req.downloadHandler.text, "\"balance\"\\s*:\\s*(\\d+)");
                balance = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                status = "Balance OK: " + balance;
            }
            else status = "Balance check failed: " + req.responseCode + " " + req.downloadHandler.text;
            Repaint();
        }

        IEnumerator<object> Generate()
        {
            string body = "{\"type\":\"text_to_model\",\"prompt\":\"" + prompt.Replace("\"", "'") +
                          "\",\"face_limit\":" + (int)faceLimit + "}";
            using var req = new UnityWebRequest("https://api.tripo3d.ai/v2/openapi/task", "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            while (!req.isDone) yield return null;
            var m = System.Text.RegularExpressions.Regex.Match(
                req.downloadHandler.text, "\"task_id\"\\s*:\\s*\"([^\"]+)\"");
            if (m.Success) { taskId = m.Groups[1].Value; status = "Task started: " + taskId; }
            else status = "Generate failed: " + req.downloadHandler.text;
            Repaint();
        }

        IEnumerator<object> Poll()
        {
            using var req = UnityWebRequest.Get("https://api.tripo3d.ai/v2/openapi/task/" + taskId);
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return req.SendWebRequest();
            while (!req.isDone) yield return null;
            string text = req.downloadHandler.text;
            var st = System.Text.RegularExpressions.Regex.Match(text, "\"status\"\\s*:\\s*\"([^\"]+)\"");
            status = "Task: " + (st.Success ? st.Groups[1].Value : text);
            var url = System.Text.RegularExpressions.Regex.Match(text, "\"(?:pbr_model|model)\"\\s*:\\s*\"([^\"]+)\"");
            if (url.Success)
            {
                using var dl = UnityWebRequest.Get(url.Groups[1].Value.Replace("\\/", "/"));
                yield return dl.SendWebRequest();
                while (!dl.isDone) yield return null;
                var dir = "Assets/ShadowCity/Models";
                System.IO.Directory.CreateDirectory(dir);
                var path = dir + "/tripo_" + taskId.Substring(0, 8) + ".glb";
                System.IO.File.WriteAllBytes(path, dl.downloadHandler.data);
                AssetDatabase.Refresh();
                status = "Downloaded → " + path;
            }
            Repaint();
        }

        // minimal editor coroutine driver
        readonly List<IEnumerator<object>> running = new();
        void EditorCoroutine(IEnumerator<object> co)
        {
            running.Add(co);
            EditorApplication.update -= Drive;
            EditorApplication.update += Drive;
        }
        void Drive()
        {
            for (int i = running.Count - 1; i >= 0; i--)
                if (!running[i].MoveNext()) running.RemoveAt(i);
            if (running.Count == 0) EditorApplication.update -= Drive;
        }
    }
}
#endif
