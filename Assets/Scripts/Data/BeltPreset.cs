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
    [Tooltip("Cantidad maxima de cartas que pueden estar en la cinta a la vez. Cada slot del pool aloja una sola carta. Si <= 0, se calcula automaticamente segun cuanto entra en el path con el spacing actual.")]
    public int capacity = 0;
    [Tooltip("Como BeltRepositionerManager calcula el 'borde interior' del belt (el que se pega al gap con las orders). CenterColumn = filtra renderers al rango X de las orders (default, bueno para arcos). NearestPortal = el portal mas cercano a las orders en Z. FullBounds = bbox completo.")]
    public BeltAnchorMode anchorMode = BeltAnchorMode.CenterColumn;
}
