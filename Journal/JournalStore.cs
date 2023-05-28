﻿using System.Collections.Generic;
using System.IO;

namespace Journal;

public class JournalStore
{
    public SaveData Data = new();

    private string _filePath;

    public JournalStore(string profileName)
    {
        string savesDirectory = "saves";
        string savesPath = Path.Combine(Journal.Instance.ModHelper.Manifest.ModFolderPath, savesDirectory);
        if (!Directory.Exists(savesPath))
        {
            Directory.CreateDirectory(savesPath);
        }
        _filePath = Path.Combine(savesDirectory, profileName + ".json");
        // TODO: Backup save
        Data = Journal.Instance.ModHelper.Storage.Load<SaveData>(_filePath, false);
        if (Data == default) 
        {
            // TODO: DON'T DO THIS IF THE FILE EXISTED
            // Create file with no entries
            Data = new SaveData
            {
                Entries = new List<Entry>() // I should probably use a default value or something
            };
        }
    }

    public void SaveToDisk()
    {
        Journal.Instance.ModHelper.Storage.Save(Data, _filePath);
    }

    public class SaveData
    {
        public List<Entry> Entries;
    }
    
    public class Entry
    {
        public string Name;
        public string Description;
        public bool HasMoreToExplore;

        public Entry()
        {
            // TODO: Translation
            Name = "New Entry";
            Description = "Write about the entry here.\n\n" +
                          "You can also create different items by leaving empty lines between them.";
            HasMoreToExplore = false;
        }
    }
}
