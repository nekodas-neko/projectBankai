using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Scatters trees along the walls and rocks and plants across the floors of a dungeon map, by copying the pieces
    // the arena scene already has (so no new art is needed, and swapping the art swaps this too).
    public static class MapScenery
    {
        const float WallInset = 1.4f;
        const float TreeSpacing = 7f;
        const float SquareUnitsPerCover = 45f;

        public static int Decorate(Transform parent, WorldMap map, Transform source)
        {
            if (source == null)
            {
                return 0;
            }

            var trees = new List<GameObject>();
            var bushes = new List<GameObject>();
            var cover = new List<GameObject>();
            for (var i = 0; i < source.childCount; i++)
            {
                var child = source.GetChild(i).gameObject;
                var name = child.name;
                if (name.StartsWith("CommonTree") || name.StartsWith("Pine"))
                {
                    trees.Add(child);
                }
                else if (name.StartsWith("Bush"))
                {
                    bushes.Add(child);
                }
                else
                {
                    cover.Add(child);
                }
            }

            var placed = 0;
            for (var z = 0; z < map.Zones.Count; z++)
            {
                var zone = map.Zones[z];
                var random = new System.Random(11 + z);
                if (trees.Count > 0)
                {
                    placed += Border(parent, zone, trees, bushes, random);
                }

                if (cover.Count > 0)
                {
                    var area = (zone.MaxX - zone.MinX) * (zone.MaxY - zone.MinY);
                    var count = Mathf.RoundToInt(area / SquareUnitsPerCover);
                    for (var i = 0; i < count; i++)
                    {
                        var x = Mathf.Lerp(zone.MinX + 3f, zone.MaxX - 3f, (float)random.NextDouble());
                        var y = Mathf.Lerp(zone.MinY + 3f, zone.MaxY - 3f, (float)random.NextDouble());
                        placed += Place(parent, cover[random.Next(cover.Count)], x, y, random, 0.8f + (float)random.NextDouble() * 0.5f) ? 1 : 0;
                    }
                }
            }

            return placed;
        }

        static int Border(Transform parent, ZoneDef zone, List<GameObject> trees, List<GameObject> bushes, System.Random random)
        {
            var placed = 0;
            for (var x = zone.MinX + WallInset; x <= zone.MaxX - WallInset; x += TreeSpacing)
            {
                placed += Edge(parent, trees, bushes, x, zone.MinY + WallInset, random);
                placed += Edge(parent, trees, bushes, x, zone.MaxY - WallInset, random);
            }

            for (var y = zone.MinY + WallInset + TreeSpacing * 0.5f; y <= zone.MaxY - WallInset; y += TreeSpacing)
            {
                placed += Edge(parent, trees, bushes, zone.MinX + WallInset, y, random);
                placed += Edge(parent, trees, bushes, zone.MaxX - WallInset, y, random);
            }

            return placed;
        }

        static int Edge(Transform parent, List<GameObject> trees, List<GameObject> bushes, float x, float y, System.Random random)
        {
            // A little jitter so the line of trees does not look ruled.
            x += (float)(random.NextDouble() - 0.5) * 2f;
            y += (float)(random.NextDouble() - 0.5) * 2f;
            var useBush = bushes.Count > 0 && random.Next(3) == 0;
            var pool = useBush ? bushes : trees;
            return Place(parent, pool[random.Next(pool.Count)], x, y, random, 0.6f + (float)random.NextDouble() * 0.3f) ? 1 : 0;
        }

        static bool Place(Transform parent, GameObject template, float x, float y, System.Random random, float scale)
        {
            var copy = Object.Instantiate(template, parent);
            copy.name = template.name;
            copy.transform.position = new Vector3(x, 0f, y);
            copy.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            copy.transform.localScale = Vector3.one * scale * (template.name.StartsWith("Rock_") ? 0.4f : 1f);
            copy.SetActive(true);
            return true;
        }
    }
}
