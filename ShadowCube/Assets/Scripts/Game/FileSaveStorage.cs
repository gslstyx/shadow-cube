using System;
using System.IO;
using ShadowCube.Core;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>本地文件存档：写入临时文件后原子替换，避免中途崩溃写坏存档</summary>
    public class FileSaveStorage : ISaveStorage
    {
        public const string DefaultFileName = "progress.json";

        public string Directory { get; }
        public string FilePath { get; }

        public FileSaveStorage(string directory = null, string fileName = DefaultFileName)
        {
            Directory = string.IsNullOrEmpty(directory) ? Application.persistentDataPath : directory;
            FilePath = Path.Combine(Directory, fileName);
        }

        public string Load()
        {
            try
            {
                return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShadowCube] 读取存档失败：{e.Message}");
                return null;
            }
        }

        public void Save(string json)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, json);

                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShadowCube] 写入存档失败：{e.Message}");
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShadowCube] 删除存档失败：{e.Message}");
            }
        }
    }
}
