using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Dresses a dungeon map as a dungeon: stone pillars with a torch on top along the walls, and rubble across the floors.
    // The pillars and torches are plain boxes; the rubble copies the rocks the arena scene already has (so swapping the art swaps
    // it too). Trees, bushes and plants are left out. Everything is registered with the map view so it fades with distance.
    public static class MapScenery
    {
        public static readonly Color PillarColor = new Color(0.50f, 0.50f, 0.57f);
        public static readonly Color TorchColor = new Color(1f, 0.55f, 0.15f);

        const float WallInset = 1.1f;
        const float PillarSpacing = 9f;
        const float PillarHeight = 2.0f;
        const float PillarWidth = 0.9f;
        const float SquareUnitsPerRubble = 120f;

        public static int Decorate(MapView view, WorldMap map, Transform source, Mesh cube, Material stone, Material glow)
        {
            var pillarMaterial = Telegraphs.Tinted(stone, PillarColor);
            var torchMaterial = Telegraphs.Tinted(glow != null ? glow : stone, TorchColor);
            var rubble = new List<GameObject>();
            for (var i = 0; source != null && i < source.childCount; i++)
            {
                var child = source.GetChild(i).gameObject;
                if (child.name.StartsWith("Rock_"))
                {
                    rubble.Add(child);
                }
            }

            var placed = 0;
            for (var z = 0; z < map.Zones.Count; z++)
            {
                var zone = map.Zones[z];
                var random = new System.Random(11 + z);
                placed += Pillars(view, zone, cube, pillarMaterial, torchMaterial);
                if (rubble.Count == 0)
                {
                    continue;
                }

                var count = Mathf.RoundToInt((zone.MaxX - zone.MinX) * (zone.MaxY - zone.MinY) / SquareUnitsPerRubble);
                for (var i = 0; i < count; i++)
                {
                    var x = Mathf.Lerp(zone.MinX + 3f, zone.MaxX - 3f, (float)random.NextDouble());
                    var y = Mathf.Lerp(zone.MinY + 3f, zone.MaxY - 3f, (float)random.NextDouble());
                    var copy = Object.Instantiate(rubble[random.Next(rubble.Count)], view.Root);
                    copy.transform.position = new Vector3(x, 0f, y);
                    copy.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    copy.transform.localScale = Vector3.one * (0.8f + (float)random.NextDouble() * 0.5f) * 0.4f;
                    copy.SetActive(true);
                    view.AddScenery(copy);
                    placed++;
                }
            }

            return placed;
        }

        static int Pillars(MapView view, ZoneDef zone, Mesh cube, Material pillar, Material torch)
        {
            var placed = 0;
            for (var x = zone.MinX + WallInset; x <= zone.MaxX - WallInset; x += PillarSpacing)
            {
                placed += Pillar(view, cube, pillar, torch, x, zone.MinY + WallInset);
                placed += Pillar(view, cube, pillar, torch, x, zone.MaxY - WallInset);
            }

            for (var y = zone.MinY + WallInset + PillarSpacing * 0.5f; y <= zone.MaxY - WallInset; y += PillarSpacing)
            {
                placed += Pillar(view, cube, pillar, torch, zone.MinX + WallInset, y);
                placed += Pillar(view, cube, pillar, torch, zone.MaxX - WallInset, y);
            }

            return placed;
        }

        static int Pillar(MapView view, Mesh cube, Material pillar, Material torch, float x, float y)
        {
            var post = Box("Pillar", view, cube, pillar, new Vector3(x, PillarHeight * 0.5f, y), new Vector3(PillarWidth, PillarHeight, PillarWidth));
            var flame = Box("Torch", view, cube, torch, new Vector3(x, PillarHeight + 0.15f, y), new Vector3(0.3f, 0.3f, 0.3f));
            flame.transform.SetParent(post.transform, true);
            view.AddScenery(post);
            return 1;
        }

        static GameObject Box(string name, MapView view, Mesh cube, Material material, Vector3 position, Vector3 scale)
        {
            var box = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            box.transform.SetParent(view.Root, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<MeshFilter>().sharedMesh = cube;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }
    }
}
