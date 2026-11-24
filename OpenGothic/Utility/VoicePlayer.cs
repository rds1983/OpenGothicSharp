using Microsoft.Xna.Framework.Audio;
using OpenGothic.Scripts;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGothic;

/// <summary>
/// Plays the voice file named by a <see cref="DialogLine"/>. This is the minimal audio path
/// F1.5 needs; J1 owns the general sound-definition system and can replace the loader.
/// </summary>
public sealed class VoicePlayer : IDisposable
{
	private readonly Assets _assets;
	private readonly Dictionary<string, SoundEffect> _cache =
		new Dictionary<string, SoundEffect>(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public VoicePlayer(Assets assets)
	{
		_assets = assets;
	}

	public void Play(DialogLine line)
	{
		var name = line?.Voice;
		if (string.IsNullOrEmpty(name) || _assets == null)
		{
			return;
		}

		if (!_cache.TryGetValue(name, out var effect))
		{
			if (_missing.Contains(name) || !_assets.TryGetBytes(name, out var bytes))
			{
				if (_missing.Add(name))
				{
					Log.Warning($"Dialog voice '{name}' is missing.");
				}

				return;
			}

			try
			{
				using var stream = new MemoryStream(bytes);
				effect = SoundEffect.FromStream(stream);
				_cache[name] = effect;
			}
			catch (Exception ex)
			{
				if (_failed.Add(name))
				{
					Log.Warning($"Unable to load dialog voice '{name}': {ex.Message}");
				}

				return;
			}
		}

		try
		{
			effect.Play();
		}
		catch (Exception ex)
		{
			if (_failed.Add(name))
			{
				Log.Warning($"Unable to play dialog voice '{name}': {ex.Message}");
			}
		}
	}

	public void Dispose()
	{
		foreach (var effect in _cache.Values)
		{
			effect.Dispose();
		}

		_cache.Clear();
	}
}
