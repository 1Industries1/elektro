using UnityEngine;
using System.Collections.Generic;

public class EconomyManager : Singleton<EconomyManager>
{
    public float gold = 1000f;
    public float maxBoostEnergy = 300f;



    public int maxRockets = 10;
    public int maxHarpoons = 5;
    public int maxTower = 5;

    public int currentRockets = 6;
    public int currentHarpoons = 5;
    public int currentTower = 0;




    private float currentBoostEnergy;
    public bool isOption2Unlocked = false; // Upgrade um auch Fuel für Deuterium + Helium-3 zu kaufen

    private Dictionary<string, float> resources = new Dictionary<string, float>();

    public delegate void OnResourcesChanged();
    public event OnResourcesChanged OnResourcesChangedEvent;

    private float lastUpdateTime = 0f;  // Zeitstempel der letzten Event-Auslösung
    public float updateInterval = 0.1f; // Intervall in Sekunden, um das Event auszulösen

    public float GetBoostEnergy() => currentBoostEnergy;
    public void SetBoostEnergy(float value) => currentBoostEnergy = Mathf.Clamp(value, 0f, maxBoostEnergy);

    protected override void Awake()
    {
        base.Awake();
        //currentBoostEnergy = maxBoostEnergy;
        currentBoostEnergy = 300f;

        // Standardressourcen initialisieren (nach neuer Sortierung)
        AddResourceType("Iron");
        AddResourceType("Nickel");
        AddResourceType("Cobalt");
        AddResourceType("Titan");
        AddResourceType("Platinum");
        AddResourceType("Deuterium");
        AddResourceType("Helium-3");
    }

    // Fügt eine neue Ressource hinzu, falls sie noch nicht existiert
    public void AddResourceType(string resourceName)
    {
        if (!resources.ContainsKey(resourceName))
        {
            resources[resourceName] = 0f;
        }
    }

    // Ressourcen hinzufügen
    public void AddResources(string resourceName, float amount)
    {
        if (!resources.ContainsKey(resourceName))
        {
            AddResourceType(resourceName);
        }

        resources[resourceName] += amount;

        // UI-Update verzögern, z. B. nur alle 0,1 Sekunden aktualisieren
        if (Time.time - lastUpdateTime > 0.1f)
        {
            OnResourcesChangedEvent?.Invoke();
            lastUpdateTime = Time.time;
        }
    }

    public void AddFragment(float amount)
    {
        string fragmentName = "Fragment";  // Der Name der Ressource (Fragment)

        // Stelle sicher, dass die Ressource existiert
        AddResourceType(fragmentName);

        // Füge die Menge hinzu
        AddResources(fragmentName, amount);
    }

    // Ressourcen ausgeben
    public bool SpendResources(string resourceName, float amount)
    {
        if (resources.ContainsKey(resourceName) && resources[resourceName] >= amount)
        {
            resources[resourceName] -= amount;
            OnResourcesChangedEvent?.Invoke();
            return true;
        }
        return false;
    }

    // Aktuellen Bestand einer Ressource abrufen
    public float GetResourceAmount(string resourceName)
    {
        return resources.ContainsKey(resourceName) ? resources[resourceName] : 0f;
    }

    public float GetGold() => gold;


    // Boost-Energie steuern
    public void HandleBoostEnergy(bool isBoosting, bool isMoving, float deltaTime)
    {
        float boostDrain = 12f;
        float moveEnergyDrain = 3f;

        float oldEnergy = currentBoostEnergy;

        if (isBoosting && currentBoostEnergy > 0)
        {
            currentBoostEnergy -= boostDrain * deltaTime;
        }
        else if (isMoving && currentBoostEnergy > 0)
        {
            currentBoostEnergy -= moveEnergyDrain * deltaTime;
        }

        if (currentBoostEnergy < 0) currentBoostEnergy = 0;

        if (oldEnergy != currentBoostEnergy)
        {
            OnResourcesChangedEvent?.Invoke();
        }
    }


    // Gibt eine Kopie des Ressourcen-Wörterbuchs zurück
    public Dictionary<string, float> GetAllResources()
    {
        return new Dictionary<string, float>(resources);
    }


    // Boost-Energie kaufen
    public bool BuyBoostEnergyWithResources()
    {
        Debug.Log("!currentBoostEnergy: " + currentBoostEnergy);
        if (currentBoostEnergy >= maxBoostEnergy)
        {
            Debug.Log("Boost-Energie ist bereits voll!");
            return false;
        }

        // Option 1: Nickel + Eisen -> 25 Fuel
        if (SpendResources("Nickel", 1) && SpendResources("Iron", 1))
        {
            SetBoostEnergy(currentBoostEnergy + 25f);
            OnResourcesChangedEvent?.Invoke();
            Debug.Log("Boost-Energie mit Nickel + Eisen gekauft!");
            return true;
        }

        // Option 2: Deuterium + Helium-3 -> 100 Fuel (nur verfügbar, wenn freigeschaltet)
        if (isOption2Unlocked && SpendResources("Deuterium", 1) && SpendResources("Helium-3", 1))
        {
            SetBoostEnergy(currentBoostEnergy + 100f);
            OnResourcesChangedEvent?.Invoke();
            Debug.Log("Boost-Energie mit Deuterium + Helium-3 gekauft!");
            return true;
        }

        Debug.Log("Nicht genug Ressourcen für Boost-Energie!");
        return false;
    }


    // Methode, um das Upgrade zu aktivieren
    public void UnlockOption2()
    {
        isOption2Unlocked = true;
        Debug.Log("Option 2 freigeschaltet!");
    }


    //////////////////////////////
    ///// Upgrades

    public void UpgradeBoostEnergy()
    {
        // Beispiel: Erhöhe die maxBoostEnergy um 50
        float upgradeAmount = 50f;

        // Prüfe, ob genügend Ressourcen vorhanden sind, um das Upgrade durchzuführen
        if (SpendResources("Nickel", 2) && SpendResources("Iron", 2))
        {
            maxBoostEnergy += upgradeAmount;
            SetBoostEnergy(maxBoostEnergy);  // Stelle sicher, dass die Boost-Energie auf das neue Maximum gesetzt wird
            Debug.Log("Boost-Energie Upgrade erfolgreich! Neuer Maximalwert: " + maxBoostEnergy);
            OnResourcesChangedEvent?.Invoke();  // Event auslösen
        }
        else
        {
            Debug.Log("Nicht genug Ressourcen für das Boost-Energie-Upgrade!");
        }
    }

    public void BuyRocket()
    {
        // Kosten einer Rakete in Ressourcen
        int ironCost = 10;
        int nickelCost = 5;
        int cobaltCost = 2;

        if (currentRockets < maxRockets &&
            SpendResources("Iron", ironCost) &&
            SpendResources("Nickel", nickelCost) &&
            SpendResources("Cobalt", cobaltCost))
        {
            currentRockets++;
            Debug.Log("Rakete gekauft! Verbleibende Ressourcen: Iron-" + GetResourceAmount("Iron") +
                      ", Nickel-" + GetResourceAmount("Nickel") + ", Cobalt-" + GetResourceAmount("Cobalt"));
        }
        else
        {
            Debug.Log("Nicht genug Ressourcen für eine Rakete!");
        }
    }

    public void BuyHarpoon()
    {
        // Neue Kosten einer Harpune
        int ironCost = 3;

        if (currentHarpoons < maxHarpoons && SpendResources("Iron", ironCost))
        {
            currentHarpoons++;
            Debug.Log("Harpune gekauft! Verbleibende Ressourcen: Iron-" + GetResourceAmount("Iron"));
        }
        else
        {
            Debug.Log("Nicht genug Ressourcen für eine Harpune!");
        }
    }

}