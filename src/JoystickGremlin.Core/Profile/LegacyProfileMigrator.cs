// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Migrates legacy profile JSON (which stored bindings inside a <c>modes</c> array)
/// to the flat <see cref="Profile"/> format.
/// </summary>
public static class LegacyProfileMigrator
{
    /// <summary>
    /// Detects whether the given JSON node is in the old modes-based format and,
    /// if so, rewrites it to the flat format before deserialization.
    /// Returns the original node unchanged if it is already in the new format.
    /// </summary>
    public static JsonNode? Migrate(JsonNode? root)
    {
        if (root is not JsonObject obj) return root;
        if (!obj.ContainsKey("modes")) return root;

        // Build a flat bindings list from all modes (first mode wins on conflict).
        var merged = new Dictionary<string, InputBinding>();

        var modes = obj["modes"] as JsonArray;
        if (modes is not null)
        {
            foreach (var modeNode in modes)
            {
                var modeObj = modeNode as JsonObject;
                var bindings = modeObj?["bindings"] as JsonArray;
                if (bindings is null) continue;

                foreach (var bindingNode in bindings)
                {
                    var bindingObj = bindingNode as JsonObject;
                    if (bindingObj is null) continue;

                    var deviceGuid = bindingObj["deviceGuid"]?.GetValue<string>() ?? string.Empty;
                    var inputType  = bindingObj["inputType"]?.GetValue<string>() ?? string.Empty;
                    var identifier = bindingObj["identifier"]?.GetValue<int>() ?? 0;

                    var key = $"{deviceGuid}:{inputType}:{identifier}";
                    if (!merged.ContainsKey(key))
                    {
                        // Build InputBinding from the raw JSON values (preserve Actions array).
                        var newBinding = new InputBinding
                        {
                            DeviceGuid = Guid.TryParse(deviceGuid, out var g) ? g : Guid.Empty,
                            InputType  = Enum.TryParse<InputType>(inputType, out var t) ? t : InputType.JoystickButton,
                            Identifier = identifier,
                        };

                        var actionsArray = bindingObj["actions"] as JsonArray;
                        if (actionsArray is not null)
                        {
                            foreach (var actionNode in actionsArray)
                            {
                                var actionObj = actionNode as JsonObject;
                                if (actionObj is null) continue;

                                var tag = actionObj["actionTag"]?.GetValue<string>() ?? string.Empty;
                                // Skip change-mode actions — they no longer exist.
                                if (string.Equals(tag, "change-mode", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                var config = actionObj["configuration"] as JsonObject;
                                newBinding.Actions.Add(new BoundAction
                                {
                                    ActionTag     = tag,
                                    Configuration = config,
                                });
                            }
                        }

                        merged[key] = newBinding;
                    }
                }
            }
        }

        // Rebuild the root object without the modes key.
        var migratedRoot = new JsonObject();
        if (obj["id"] is { } id)
            migratedRoot["id"] = id.DeepClone();
        if (obj["name"] is { } name)
            migratedRoot["name"] = name.DeepClone();

        var bindingsJson = new JsonArray();
        foreach (var binding in merged.Values)
        {
            var bObj = new JsonObject
            {
                ["deviceGuid"] = binding.DeviceGuid.ToString(),
                ["inputType"]  = binding.InputType.ToString(),
                ["identifier"] = binding.Identifier,
            };
            var actArr = new JsonArray();
            foreach (var action in binding.Actions)
            {
                var aObj = new JsonObject { ["actionTag"] = action.ActionTag };
                if (action.Configuration is not null)
                    aObj["configuration"] = action.Configuration.DeepClone();
                actArr.Add(aObj);
            }
            bObj["actions"] = actArr;
            bindingsJson.Add(bObj);
        }
        migratedRoot["bindings"] = bindingsJson;

        return migratedRoot;
    }
}
