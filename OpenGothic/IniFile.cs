using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenGothic;

public class IniFile
{
	private sealed class Section
	{
		public string Name { get; set; }
		public List<KeyValuePair<string, string>> Values { get; } = new List<KeyValuePair<string, string>>();
	}

	private static readonly Encoding Encoding = Encoding.Latin1;

	private readonly List<Section> _sections = new List<Section>();

	public bool IsDirty { get; private set; }

	public void Load(string path)
	{
		if (!File.Exists(path))
		{
			Log.Warning($"Settings file not found, using defaults: '{path}'.");
			return;
		}

		string text;
		try
		{
			text = File.ReadAllText(path, Encoding);
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to read settings file '{path}': {ex.Message}");
			return;
		}

		Parse(text);
	}

	public void Parse(string text)
	{
		_sections.Clear();

		Section current = null;

		foreach (var rawLine in text.Split('\n'))
		{
			var line = rawLine.Trim();

			if (line.Length == 0 || line[0] == ';')
			{
				continue;
			}

			if (line[0] == '[')
			{
				var end = line.IndexOf(']');
				var name = end > 0 ? line.Substring(1, end - 1) : line.Substring(1);
				current = GetOrAddSection(name.Trim());
				continue;
			}

			if (current == null)
			{
				continue;
			}

			var eq = line.IndexOf('=');
			if (eq <= 0)
			{
				continue;
			}

			var key = line.Substring(0, eq).Trim();
			var value = line.Substring(eq + 1);

			var comment = value.IndexOf(';');
			if (comment >= 0)
			{
				value = value.Substring(0, comment);
			}

			current.Values.Add(new KeyValuePair<string, string>(key, value.Trim()));
		}
	}

	public bool Has(string section, string name) => FindValue(section, name) != null;

	public string GetString(string section, string name, string defaultValue = "")
	{
		return FindValue(section, name) ?? defaultValue;
	}

	public int GetInt(string section, string name, int defaultValue = 0)
	{
		var value = FindValue(section, name);
		return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
	}

	public float GetFloat(string section, string name, float defaultValue = 0)
	{
		var value = FindValue(section, name);
		return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
	}

	public void Set(string section, string name, string value)
	{
		var index = FindValueIndex(section, name);
		var s = GetOrAddSection(section);
		if (index >= 0)
		{
			s.Values[index] = new KeyValuePair<string, string>(s.Values[index].Key, value);
		}
		else
		{
			s.Values.Add(new KeyValuePair<string, string>(name, value));
		}

		IsDirty = true;
	}

	public void Set(string section, string name, int value) =>
		Set(section, name, value.ToString(CultureInfo.InvariantCulture));

	public void Set(string section, string name, float value) =>
		Set(section, name, value.ToString("R", CultureInfo.InvariantCulture));

	public void Save(string path)
	{
		var sb = new StringBuilder();

		foreach (var section in _sections)
		{
			sb.Append('[').Append(section.Name).Append(']').Append("\r\n\r\n");

			foreach (var pair in section.Values)
			{
				sb.Append(pair.Key).Append('=').Append(pair.Value).Append("\r\n");
			}

			sb.Append("\r\n");
		}

		try
		{
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllText(path, sb.ToString(), Encoding);
			IsDirty = false;
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to write settings file '{path}': {ex.Message}");
		}
	}

	private Section FindSection(string name)
	{
		foreach (var section in _sections)
		{
			if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return section;
			}
		}

		return null;
	}

	private Section GetOrAddSection(string name)
	{
		var section = FindSection(name);
		if (section == null)
		{
			section = new Section { Name = name };
			_sections.Add(section);
		}

		return section;
	}

	private string FindValue(string sectionName, string name)
	{
		var section = FindSection(sectionName);
		if (section == null)
		{
			return null;
		}

		foreach (var pair in section.Values)
		{
			if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
			{
				return pair.Value;
			}
		}

		return null;
	}

	private int FindValueIndex(string sectionName, string name)
	{
		var section = FindSection(sectionName);
		if (section == null)
		{
			return -1;
		}

		for (var i = 0; i < section.Values.Count; ++i)
		{
			if (string.Equals(section.Values[i].Key, name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		return -1;
	}
}
