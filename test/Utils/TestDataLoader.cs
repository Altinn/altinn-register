using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Register.Tests.IntegrationTests;

namespace Altinn.Register.Tests.Utils
{
    public static class TestDataLoader
    {
        public static async Task<T> Load<T>(string id)
        {
            string path = $"../Testdata/{typeof(T).Name}/{id}.json";
            string fileContent = await File.ReadAllTextAsync(GetPath(id));
            T data = JsonSerializer.Deserialize<T>(fileContent);
            return data;
        }

        private static string GetPath(string id)
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PartiesControllerTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, "Testdata", $"{id}.json");
        }
    }
}
