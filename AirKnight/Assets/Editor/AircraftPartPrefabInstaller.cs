using UnityEditor;
using UnityEngine;

public static class AircraftPartPrefabInstaller
{
    const string PrefabPath = "Assets/Prefabs/GenericAircraft.prefab";

    [InitializeOnLoadMethod]
    static void ScheduleInstall()
    {
        EditorApplication.delayCall += InstallIfNeeded;
    }

    [MenuItem("Tools/MVP/Install Aircraft Part Status Components")]
    public static void InstallIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) return;

        bool changed = false;
        try
        {
            FuselagePartStatus fuselage = GetOrAdd<FuselagePartStatus>(root, ref changed, out bool fuselageAdded);
            if (fuselageAdded)
            {
                fuselage.partName = "Generic Fuselage";
                fuselage.weight = 400f;
                fuselage.hitPoints = 400f;
            }

            MainWingPartStatus mainWing = GetOrAdd<MainWingPartStatus>(root, ref changed, out bool mainWingAdded);
            if (mainWingAdded)
            {
                mainWing.partName = "Generic Main Wing";
                mainWing.weight = 300f;
                mainWing.hitPoints = 300f;
                mainWing.width = 10f;
                mainWing.hardpointCount = 0;
                mainWing.maximumHardpointWeight = 0f;
            }

            ControlSurfacePartStatus controlSurface = GetOrAdd<ControlSurfacePartStatus>(root, ref changed, out bool controlAdded);
            if (controlAdded)
            {
                controlSurface.partName = "Generic Control Surface";
                controlSurface.weight = 40f;
                controlSurface.hitPoints = 50f;
            }

            AuxiliaryWingPartStatus auxiliaryWing = GetOrAdd<AuxiliaryWingPartStatus>(root, ref changed, out bool auxiliaryWingAdded);
            if (auxiliaryWingAdded)
            {
                auxiliaryWing.partName = "Generic Tail";
                auxiliaryWing.wingCount = 2;
                auxiliaryWing.weight = 25f;
                auxiliaryWing.hitPoints = 25f;
            }

            EnginePartStatus engine = GetOrAdd<EnginePartStatus>(root, ref changed, out bool engineAdded);
            if (engineAdded)
            {
                engine.partName = "Generic Engine";
                engine.weight = 200f;
                engine.hitPoints = 150f;
                engine.thrust = 20000f;
            }

            FuelTankPartStatus fuelTank = GetOrAdd<FuelTankPartStatus>(root, ref changed, out bool fuelAdded);
            if (fuelAdded)
            {
                fuelTank.partName = "Generic Fuel Tank";
                fuelTank.weight = 100f;
                fuelTank.hitPoints = 50f;
            }

            ArmorPartStatus armor = GetOrAdd<ArmorPartStatus>(root, ref changed, out bool armorAdded);
            if (armorAdded)
            {
                armor.partName = "Generic Armor";
                armor.weight = 50f;
                armor.hitPoints = 100f;
            }

            AuxiliaryEquipmentPartStatus equipment = GetOrAdd<AuxiliaryEquipmentPartStatus>(root, ref changed, out bool equipmentAdded);
            if (equipmentAdded)
            {
                equipment.partName = "Generic Sensor";
                equipment.weight = 20f;
                equipment.hitPoints = 30f;
            }

            HardpointPartStatus hardpoint = GetOrAdd<HardpointPartStatus>(root, ref changed, out bool hardpointAdded);
            if (hardpointAdded)
            {
                hardpoint.partName = "Generic Hardpoint";
                hardpoint.hardpointCount = 2;
                hardpoint.weight = 10f;
                hardpoint.hitPoints = 20f;
                hardpoint.maximumWeaponWeight = 250f;
            }

            AircraftPartStatusConverter converter = GetOrAdd<AircraftPartStatusConverter>(root, ref changed, out _);
            converter.Calc();
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Aircraft part status components installed on GenericAircraft.prefab.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static T GetOrAdd<T>(GameObject root, ref bool changed, out bool added) where T : Component
    {
        T component = root.GetComponent<T>();
        added = component == null;
        if (added)
        {
            component = root.AddComponent<T>();
            changed = true;
        }
        return component;
    }
}
