using System;
using System.IO;
using System.Text;

namespace Kxnrl.Vanessa;

internal class Configurations
{
    public bool IsFirstLoad { get; private set; }

    private readonly string _path;

    public Configurations()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vanessa");
        Directory.CreateDirectory(dir);

        _path = Path.Combine(dir, "config.json");

        IsFirstLoad = File.Exists(_path);

        File.WriteAllText(_path, "{}", Encoding.UTF8);
    }

    public void Save()
    {
    }
}