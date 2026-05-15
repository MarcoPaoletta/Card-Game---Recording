using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utilidades compartidas entre los componentes de la cinta.
/// </summary>
public static class BeltContainers
{
    /// <summary>
    /// Devuelve (o crea, si no existe) un child con el nombre dado bajo <paramref name="parent"/>.
    /// </summary>
    public static Transform EnsureChildContainer(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        return t;
    }

    /// <summary>
    /// Detach + destroy. En play mode Destroy() es diferido al final del frame,
    /// asi que sin detach los children siguen contando en childCount y rompen
    /// los rebuilds que corren en el mismo frame.
    /// </summary>
    public static void ClearChildrenSafely(Transform parent)
    {
        if (parent == null) return;
        var snapshot = new List<Transform>(parent.childCount);
        for (int i = 0; i < parent.childCount; i++) snapshot.Add(parent.GetChild(i));
        foreach (var c in snapshot)
        {
            if (c == null) continue;
            c.SetParent(null, worldPositionStays: false);
            if (Application.isPlaying) Object.Destroy(c.gameObject);
            else Object.DestroyImmediate(c.gameObject);
        }
    }
}
