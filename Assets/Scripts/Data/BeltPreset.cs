using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preset de cinta transportadora: lista de puntos en espacio local del
/// ConveyorBelt root. El primer y ultimo punto son los portales (extremos),
/// los intermedios son partes de cinta. Minimo 2 puntos.
/// Los assets viven en Assets/Resources/BeltPresets/ para que Resources.Load
/// funcione tanto en editor como en builds.
/// </summary>
[CreateAssetMenu(fileName = "BeltPreset", menuName = "CardGame/Belt Preset")]
public class BeltPreset : ScriptableObject
{
    public string presetName = "New Belt";
    public List<Vector3> localPoints = new List<Vector3>();
}
