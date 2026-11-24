using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using OpenGothic.Scripts;
using System;
using System.Text;

namespace OpenGothic.UI;

/// <summary>
/// Bottom dialog panel: NPC name, the recent spoken lines, and the selectable entries. It is
/// a usability-first version of the reference's DialogMenu; timing, voice playback and the
/// dialog camera arrive with the world-bound NPC layer.
/// </summary>
public sealed class DialogPanel : VerticalStackPanel
{
	private readonly Label _speaker;
	private readonly Label _line;
	private readonly VerticalStackPanel _choices;

	private DialogSession _session;
	private Action<int> _select;
	private Action _close;

	/// <summary>0-based entry the keyboard cursor is on; drawn with a leading marker.</summary>
	public int SelectedIndex { get; set; }

	public DialogPanel()
	{
		Spacing = 4;
		Padding = new Thickness(14, 10);
		Background = new SolidBrush("#000000C8");
		Width = 720;
		HorizontalAlignment = HorizontalAlignment.Center;
		VerticalAlignment = VerticalAlignment.Bottom;
		Top = -12;
		Visible = false;

		_speaker = new Label { TextColor = new Color(255, 204, 102) };
		_line = new Label
		{
			TextColor = new Color(232, 232, 232),
			Wrap = true,
			Width = 692
		};
		_choices = new VerticalStackPanel { Spacing = 2 };

		Widgets.Add(_speaker);
		Widgets.Add(_line);
		Widgets.Add(_choices);
	}

	public void Show(DialogSession session, Action<int> select, Action close)
	{
		_session = session;
		_select = select;
		_close = close;
		Refresh();
	}

	public void Hide()
	{
		_session = null;
		_select = null;
		_close = null;
		Visible = false;
	}

	/// <summary>Rebuilds the panel after a choice; called on dialog steps, never per frame.</summary>
	public void Refresh()
	{
		if (_session == null)
		{
			Visible = false;
			return;
		}

		_speaker.Text = _session.NpcName;
		_line.Text = BuildTranscript();
		_choices.Widgets.Clear();

		var choices = _session.Choices;
		for (var i = 0; i < choices.Count; ++i)
		{
			var index = i;
			var text = string.IsNullOrEmpty(choices[i].Text) ? "(continue)" : choices[i].Text;
			var marker = i == SelectedIndex ? "> " : "  ";
			var button = Button.CreateTextButton($"{marker}{i + 1}. {text}");
			button.Click += (_, _) => _select?.Invoke(index);
			_choices.Widgets.Add(button);
		}

		if (_session.Finished)
		{
			var close = Button.CreateTextButton("Close (Esc)");
			close.Click += (_, _) => _close?.Invoke();
			_choices.Widgets.Add(close);
		}

		Visible = true;
	}

	private string BuildTranscript()
	{
		var lines = _session.Lines;
		if (lines.Count == 0)
		{
			return _session.Finished ? "(no answer)" : "...";
		}

		var text = new StringBuilder();
		var start = Math.Max(0, lines.Count - 4);
		for (var i = start; i < lines.Count; ++i)
		{
			if (text.Length > 0)
			{
				text.Append('\n');
			}

			var line = lines[i];
			var spoken = string.IsNullOrEmpty(line.Text) ? $"<{line.Unit}>" : line.Text;
			text.Append(line.Speaker).Append(": ").Append(spoken);
		}

		return text.ToString();
	}
}
