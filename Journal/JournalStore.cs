using System.Collections.Generic;
using System.IO;
using OWML.Common;

namespace Journal;

public class JournalStore
{
    public SaveData Data = new();

    private string _filePath;
    private bool _backupOnNextSave;

    public JournalStore(string profileName)
    {
        string savesDirectory = "saves";
        string savesPath = Path.Combine(Journal.Instance.ModHelper.Manifest.ModFolderPath, savesDirectory);
        if (!Directory.Exists(savesPath))
        {
            Directory.CreateDirectory(savesPath);
        }
        _filePath = Path.Combine(savesDirectory, profileName + ".json");
        Data = Journal.Instance.ModHelper.Storage.Load<SaveData>(_filePath, false);
        if (Data == default) 
        {
            string fullFilePath = GetFullFilePath();
            if (File.Exists(fullFilePath))
            {
                Journal.Instance.ModHelper.Console.WriteLine(
                    $"Save file {fullFilePath} found but with unexpected format, corrupted?\n" +
                    "The file is now renamed (added .corrupted extension) in case you want to manually fix it, " +
                    "the journal will start empty now", MessageType.Error);
                string corruptedPath = fullFilePath + ".corrupted";
                File.Delete(corruptedPath);
                File.Move(fullFilePath, corruptedPath);
            }
            // Create file with no entries
            Data = new SaveData();
            // No need to save empty file to disk...
        }
        else
        {
            _backupOnNextSave = true;
        }
    }

    private string GetFullFilePath()
    {
        // Really messy all the used combinations...
        return Path.Combine(Journal.Instance.ModHelper.Manifest.ModFolderPath, _filePath);
    }

    public void SaveToDisk()
    {
        if (_backupOnNextSave)
        {
            string fullFilePath = GetFullFilePath();
            File.Copy(fullFilePath, fullFilePath + ".old", true);
            _backupOnNextSave = false;
        }
        Journal.Instance.ModHelper.Storage.Save(Data, _filePath);
    }

    public class SaveData
    {
        public bool ShowPrompts;
        public List<Entry> Entries;

        public SaveData()
        {
            ShowPrompts = true;
            Entries = new List<Entry>();
        }
    }
    
    public class Entry
    {
        public string Name;
        public string Description;
        public bool HasMoreToExplore;
        public string EpicasAlbumSnapshotName;

        public Entry()
        {
            // TODO: Translation
            Name = "New Entry";
            Description = "Write about the entry here.\n\n" +
                          "You can also create different items by leaving empty lines between them.";
            HasMoreToExplore = false;
            EpicasAlbumSnapshotName = null;
        }
    }
}
