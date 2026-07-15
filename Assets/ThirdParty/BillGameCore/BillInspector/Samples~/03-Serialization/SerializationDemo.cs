using UnityEngine;
using BillInspector;
using System.Collections.Generic;

/// <summary>
/// Demo: Dictionary, HashSet, and Tuple serialization.
/// </summary>
public class SerializationDemo : BillSerializedMonoBehaviour
{
    [BillTitle("Serialization Demo", "Dictionary, HashSet, and Tuple support")]

    [BillDictionaryDrawer(KeyLabel = "Item", ValueLabel = "Count")]
    public Dictionary<string, int> inventory = new()
    {
        { "Sword", 1 },
        { "Potion", 5 },
        { "Arrow", 20 }
    };

    public HashSet<string> discoveredAreas = new() { "Forest", "Cave", "Village" };

    public (string name, int level) playerInfo = ("Hero", 1);

    [BillButton("Add Random Item")]
    void AddItem()
    {
        string[] items = { "Gem", "Shield", "Ring", "Scroll", "Herb" };
        var item = items[Random.Range(0, items.Length)];
        if (inventory.ContainsKey(item))
            inventory[item]++;
        else
            inventory[item] = 1;
    }
}
