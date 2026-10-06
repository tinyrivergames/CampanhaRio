using System.IO;
using CampanhaRio.Jobs;
using UnityEditor;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// The jobs of the vertical slice (PLANO_CAMPANHA 6: the Rio do Moinho with two jobs) and the catalog the board reads:
    ///   Data/Jobs/Job_CartaUrgente.asset   the urgent letter (job 1, from the start)
    ///   Data/Jobs/Job_BodeMedroso.asset    the scared goat (after the letter)
    ///   Resources/JobCatalog.asset
    /// Re-running keeps the assets (and their GUIDs) and rewrites their values.
    /// Menu: CampanhaRio > Setup > Build Jobs. Batch: CampanhaRio.Editor.JobsBuilder.Build
    /// </summary>
    public static class JobsBuilder
    {
        const string JobsDir = "Assets/_Project/Data/Jobs";
        const string CatalogPath = "Assets/_Project/Resources/JobCatalog.asset";

        [MenuItem("CampanhaRio/Setup/Build Jobs")]
        public static void Build()
        {
            var letter = Job("Job_CartaUrgente", j =>
            {
                j.id = "carta_urgente";
                j.title = "The urgent letter";
                j.client = "The mayor of Vila do Moinho";
                j.description = "The village is cut off since the storm: this letter must get there before sunset. Keep it dry!";
                j.kind = JobKind.Delivery;
                j.rule = JobRule.UrgentLetter;
                j.riverId = "Rio_Moinho";
                j.timeLimit = 240f;
                j.timeStarShare = 0.8f;
                j.grandmaTime = 165f;
                j.conditionStar = 0.6f;
                j.pay = 40;
                j.payPerStar = 10;
                j.reputation = 1;
            });
            var goat = Job("Job_BodeMedroso", j =>
            {
                j.id = "bode_medroso";
                j.title = "The scared goat";
                j.client = "Dona Cabra, the cheese maker";
                j.description = "Her goat must get to the fair downstream. He is scared of speed and jumps: if he panics, he jumps out!";
                j.kind = JobKind.Passenger;
                j.rule = JobRule.ScaredGoat;
                j.riverId = "Rio_Moinho";
                j.timeLimit = 300f;
                j.timeStarShare = 0.85f;
                j.grandmaTime = 210f;
                j.conditionStar = 0.5f;
                j.pay = 50;
                j.payPerStar = 12;
                j.reputation = 1;
            });

            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var catalog = AssetDatabase.LoadAssetAtPath<JobCatalog>(CatalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<JobCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.entries.Clear();
            catalog.entries.Add(new JobCatalog.Entry { job = letter });
            catalog.entries.Add(new JobCatalog.Entry { job = goat, requires = { letter } });
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Campanha] Jobs built: {catalog.entries.Count} in {CatalogPath}");
        }

        static JobDefinition Job(string asset, System.Action<JobDefinition> set)
        {
            Directory.CreateDirectory(JobsDir);
            string path = $"{JobsDir}/{asset}.asset";
            var job = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
            if (!job) { job = ScriptableObject.CreateInstance<JobDefinition>(); AssetDatabase.CreateAsset(job, path); }
            set(job);
            EditorUtility.SetDirty(job);
            return job;
        }
    }
}
