using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Game.Core
{
    // Changes ability and class numbers from a plain text file, so a balance change needs no rebuild.
    //
    //   # comments and blank lines are ignored
    //   ability.king_smash.damage = 25
    //   ability.fireball.variants.frost.damageScale = 0.9
    //   class.staff.stats.maxHealth = 75
    //   class.slime_boss.phases.1.speedScale = 1.4
    //
    // The first part is "ability" or "class", then the id, then a path of property names (any letter case).
    // A name that leads to a list takes the next part as a position (0, 1, ...) or as the id of the entry.
    public static class Tuning
    {
        const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        public sealed class Entry
        {
            public int Line;
            public string Kind;
            public string Id;
            public string[] Path;
            public string Value;
            public string Text;
        }

        public static List<Entry> Parse(string text, List<string> errors)
        {
            var entries = new List<Entry>();
            if (string.IsNullOrEmpty(text))
            {
                return entries;
            }

            var lines = text.Replace("\r", string.Empty).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var hash = line.IndexOf('#');
                if (hash >= 0)
                {
                    line = line.Substring(0, hash);
                }

                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    errors.Add($"tuning line {i + 1}: expected 'ability.id.field = value'");
                    continue;
                }

                var key = line.Substring(0, equals).Trim();
                var parts = key.Split('.');
                var kind = parts[0].ToLowerInvariant();
                if (parts.Length < 3 || (kind != "ability" && kind != "class"))
                {
                    errors.Add($"tuning line {i + 1}: '{key}' must look like ability.id.field or class.id.field");
                    continue;
                }

                var path = new string[parts.Length - 2];
                Array.Copy(parts, 2, path, 0, path.Length);
                entries.Add(new Entry { Line = i + 1, Kind = kind, Id = parts[1], Path = path, Value = line.Substring(equals + 1).Trim(), Text = key });
            }

            return entries;
        }

        public static void Apply(List<Entry> entries, Registry<AbilityDef> abilities, Registry<ClassDef> classes, List<string> errors)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                object target;
                if (entry.Kind == "ability")
                {
                    if (!abilities.TryGet(entry.Id, out var ability))
                    {
                        errors.Add($"tuning line {entry.Line}: unknown ability '{entry.Id}'");
                        continue;
                    }

                    target = ability;
                }
                else
                {
                    if (!classes.TryGet(entry.Id, out var def))
                    {
                        errors.Add($"tuning line {entry.Line}: unknown class '{entry.Id}'");
                        continue;
                    }

                    target = def;
                }

                var message = SetPath(target, entry.Path, 0, entry.Value, out _);
                if (message != null)
                {
                    errors.Add($"tuning line {entry.Line}: {entry.Text}: {message}");
                }
            }
        }

        // Returns an error message, or null when the value was set. A struct is boxed, so the changed copy comes back in "updated".
        static string SetPath(object holder, string[] path, int index, string value, out object updated)
        {
            updated = holder;
            var type = holder.GetType();
            var name = path[index];
            var property = type.GetProperty(name, Members);
            var field = property == null ? type.GetField(name, Members) : null;
            if (property == null && field == null)
            {
                return $"{type.Name} has no setting '{name}'";
            }

            var memberType = property != null ? property.PropertyType : field.FieldType;
            var memberName = property != null ? property.Name : field.Name;
            if (memberName == "Id")
            {
                return "the id cannot be changed";
            }

            if (index == path.Length - 1)
            {
                if (!TryConvert(value, memberType, out var converted, out var problem))
                {
                    return problem;
                }

                if (property != null)
                {
                    if (!property.CanWrite)
                    {
                        return $"'{name}' cannot be changed";
                    }

                    property.SetValue(holder, converted);
                }
                else
                {
                    field.SetValue(holder, converted);
                }

                return null;
            }

            var current = property != null ? property.GetValue(holder) : field.GetValue(holder);
            if (current == null)
            {
                return $"'{name}' is not set on this definition";
            }

            if (current is Array array)
            {
                if (index + 1 >= path.Length)
                {
                    return $"'{name}' is a list: name an entry and a setting";
                }

                var element = FindElement(array, path[index + 1]);
                if (element == null)
                {
                    return $"'{name}' has no entry '{path[index + 1]}'";
                }

                if (index + 2 >= path.Length)
                {
                    return $"'{name}.{path[index + 1]}' needs a setting after it";
                }

                return SetPath(element, path, index + 2, value, out _);
            }

            var message = SetPath(current, path, index + 1, value, out var changed);
            if (message != null)
            {
                return message;
            }

            if (memberType.IsValueType)
            {
                if (property != null)
                {
                    property.SetValue(holder, changed);
                }
                else
                {
                    field.SetValue(holder, changed);
                }
            }

            return null;
        }

        static object FindElement(IList list, string key)
        {
            if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var position))
            {
                return position >= 0 && position < list.Count ? list[position] : null;
            }

            for (var i = 0; i < list.Count; i++)
            {
                var idProperty = list[i]?.GetType().GetProperty("Id", Members);
                if (idProperty != null && string.Equals(idProperty.GetValue(list[i]) as string, key, StringComparison.OrdinalIgnoreCase))
                {
                    return list[i];
                }
            }

            return null;
        }

        static bool TryConvert(string text, Type type, out object value, out string problem)
        {
            value = null;
            problem = null;
            if (type == typeof(float))
            {
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && !float.IsNaN(number) && !float.IsInfinity(number))
                {
                    value = number;
                    return true;
                }

                problem = $"'{text}' is not a number";
                return false;
            }

            if (type == typeof(int))
            {
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
                {
                    value = whole;
                    return true;
                }

                problem = $"'{text}' is not a whole number";
                return false;
            }

            if (type == typeof(bool))
            {
                if (bool.TryParse(text, out var flag))
                {
                    value = flag;
                    return true;
                }

                problem = $"'{text}' is not true or false";
                return false;
            }

            if (type == typeof(string))
            {
                value = text;
                return true;
            }

            if (type.IsEnum)
            {
                try
                {
                    value = Enum.Parse(type, text, true);
                    return true;
                }
                catch (ArgumentException)
                {
                    problem = $"'{text}' is not one of {string.Join(", ", Enum.GetNames(type))}";
                    return false;
                }
            }

            problem = $"a {type.Name} cannot be set from the file";
            return false;
        }
    }
}
