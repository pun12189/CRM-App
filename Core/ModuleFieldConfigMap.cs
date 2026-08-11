using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Models;

namespace Tijori.Core
{
    public class ModuleFieldConfigMap
    {
        private readonly Dictionary<string, CustomFieldDefinition> _fields = new();

        public ModuleFieldConfigMap(IEnumerable<CustomFieldDefinition> definitions)
        {
            foreach (var def in definitions)
            {
                // Key by C# Property Name (FieldName)
                if (!string.IsNullOrEmpty(def.FieldName))
                {
                    _fields[def.FieldName] = def;
                }
            }
        }

        // Returns DisplayLabel if set by admin, otherwise falls back to default prompt
        public string GetLabel(string propertyName, string defaultPrompt)
        {
            if (_fields.TryGetValue(propertyName, out var def) && !string.IsNullOrWhiteSpace(def.DisplayLabel))
            {
                return def.DisplayLabel;
            }
            return defaultPrompt;
        }

        // Returns visibility state (Defaults to true if field definition is missing)
        public bool GetIsVisible(string propertyName)
        {
            if (_fields.TryGetValue(propertyName, out var def))
            {
                return def.IsVisible;
            }
            return false; // Default fallback
        }

        // Returns required validation state
        public bool GetIsRequired(string propertyName)
        {
            if (_fields.TryGetValue(propertyName, out var def))
            {
                return def.IsRequired;
            }
            return false;
        }
    }
}
