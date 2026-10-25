using UnityEngine;

namespace KnocksFromBeneath
{
// A-01: звук предмета по материалу. Материал угадывается по имени объекта (сцена не меняется):
// коробки — картон, посуда — фарфор, сковородки/кастрюли — металл, мешки — мягкое и т.д.
public static class ItemAudio
{
    public enum Material { Generic, Cardboard, Paper, Plate, Metal, Glass, Wood, Soft }

    public static Material Guess(Transform item)
    {
        string n = item.name.ToLowerInvariant();
        if (item.parent != null) n += " " + item.parent.name.ToLowerInvariant();
        if (Has(n, "box", "carton", "package", "parcel")) return Material.Cardboard;
        if (Has(n, "book", "paper", "note", "letter", "magazine", "newspaper", "записк")) return Material.Paper;
        if (Has(n, "sarten", "pan", "pot", "lid", "hammer", "crowbar", "can", "metal", "kettle", "tool")) return Material.Metal;
        if (Has(n, "cup", "mug", "plate", "bowl", "dish", "cake", "pie", "ceramic", "vase")) return Material.Plate;
        if (Has(n, "glass", "bottle", "jar")) return Material.Glass;
        if (Has(n, "bag", "trash", "garbage", "pillow", "cloth", "towel", "мусор", "пакет")) return Material.Soft;
        if (Has(n, "wood", "plank", "board", "chair", "stool", "frame", "remote", "radio")) return Material.Wood;
        return Material.Generic;
    }

    static bool Has(string text, params string[] parts)
    {
        foreach (string p in parts) if (text.Contains(p)) return true;
        return false;
    }

    public static AudioClip[] Drops(Material material)
    {
        var lib = GameSounds.Library;
        if (lib == null) return null;
        switch (material)
        {
            case Material.Cardboard: return lib.cardboardDrop;
            case Material.Paper: return lib.paperDrop;
            case Material.Plate: return lib.plateDrop;
            case Material.Metal: return lib.metalDrop;
            case Material.Glass: return lib.glassDrop;
            case Material.Wood: return lib.woodDrop;
            case Material.Soft: return lib.softDrop;
            default: return lib.woodDrop;
        }
    }

    static AudioClip[] Grabs(Material material)
    {
        var lib = GameSounds.Library;
        if (lib == null) return null;
        return material == Material.Paper ? lib.paperGrab : material == Material.Metal ? lib.metalGrab : lib.grab;
    }

    public static void Apply(ItemFoley foley)
    {
        if (foley == null || GameSounds.Library == null) return;
        Material m = Guess(foley.transform);
        AudioClip[] drops = Drops(m);
        foley.SetClips(GameSounds.Pick(Grabs(m)), GameSounds.Pick(drops), GameSounds.Pick(GameSounds.Library.cloth));
    }

    public static void Apply(ImpactSounds impact)
    {
        if (impact == null || GameSounds.Library == null) return;
        AudioClip[] drops = Drops(Guess(impact.transform));
        if (drops != null && drops.Length > 0) impact.clips = drops;
        if (impact.impactSource != null) AudioRouting.Assign(impact.impactSource, AudioBus.Foley);
    }
}
}
