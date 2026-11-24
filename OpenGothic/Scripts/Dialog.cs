using System;
using System.Collections.Generic;
using OpenGothic.Vobs;
using ZenKit.Daedalus;

namespace OpenGothic.Scripts;

/// <summary>One selectable line in a dialog. The description is the menu text.</summary>
public sealed class DialogChoice
{
	public string Text { get; init; }
	public int Function { get; init; }
	public int Sort { get; init; }
	public bool IsTrade { get; init; }

	internal InfoInstance Info { get; init; }
}

/// <summary>
/// One spoken line. <see cref="Unit"/> is the script's output-unit name and doubles as the
/// voice hook: the reference plays <c>&lt;Unit&gt;.wav</c> for it.
/// </summary>
public sealed class DialogLine
{
	public string Speaker { get; init; }
	public string Text { get; init; }
	public string Unit { get; init; }
	public bool IsPlayer { get; init; }

	public string Voice => string.IsNullOrEmpty(Unit) ? null : Unit + ".wav";
}

/// <summary>
/// Drives one conversation with a script NPC. The flow mirrors Try/OpenGothic's
/// <c>DialogMenu</c> plus <c>GameScript::dialogChoices</c>/<c>updateDialog</c>: choices come
/// from the NPC's C_INFO list, executing one runs its information function, and the
/// function's <c>Info_AddChoice</c> calls become the next menu. <c>Ai_StopProcessInfos</c>
/// ends the conversation.
/// </summary>
public sealed class DialogSession
{
	private readonly ScriptRuntime _runtime;
	private readonly NpcBinding _npc;
	private readonly List<DialogChoice> _choices = new List<DialogChoice>();
	private readonly List<DialogLine> _lines = new List<DialogLine>();

	internal readonly HashSet<int> Except = new HashSet<int>();

	private bool _finished;

	internal DialogSession(ScriptRuntime runtime, NpcBinding npc)
	{
		_runtime = runtime;
		_npc = npc;
	}

	public NpcVob Npc => _npc.Vob;

	public string NpcName => _npc.Vob.Name;

	internal NpcBinding Binding => _npc;

	/// <summary>Choices to show now; empty while a phrase is running or after the end.</summary>
	public IReadOnlyList<DialogChoice> Choices => _choices;

	/// <summary>Every line spoken in this conversation, oldest first.</summary>
	public IReadOnlyList<DialogLine> Lines => _lines;

	public bool Finished => _finished;

	public bool AwaitingChoice => !_finished && _choices.Count > 0;

	internal void Begin()
	{
		SetChoices(_runtime.BuildChoices(this, true));

		// An important dialog is not shown as a menu entry: it starts immediately, exactly
		// like DialogMenu::onStart does for a choice with an empty title.
		if (_choices.Count > 0 && string.IsNullOrEmpty(_choices[0].Text))
		{
			Enter(0);
		}
		else if (_choices.Count == 0)
		{
			Finish();
		}
	}

	/// <summary>Runs the choice at <paramref name="index"/> and advances the conversation.</summary>
	public bool Select(int index)
	{
		if (_finished || index < 0 || index >= _choices.Count)
		{
			return false;
		}

		Enter(index);
		return true;
	}

	/// <summary>
	/// Picks each distinct entry once until the script ends the conversation. Used by the
	/// <c>-talk</c> diagnostic so a full dialog can run without input; the interactive UI
	/// still allows an entry to be chosen repeatedly.
	/// </summary>
	public string RunToCompletion(int maxSteps = 32)
	{
		var tried = new HashSet<int>();
		var steps = 0;
		while (!_finished && steps < maxSteps)
		{
			var index = -1;
			for (var i = 0; i < _choices.Count; ++i)
			{
				if (tried.Add(_choices[i].Function))
				{
					index = i;
					break;
				}
			}

			if (index < 0)
			{
				break;
			}

			++steps;
			Select(index);
		}

		return _finished
			? $"dialog with '{NpcName}' completed in {steps} step(s), {_lines.Count} line(s)"
			: $"dialog with '{NpcName}' stopped after {steps} step(s), {_lines.Count} line(s)";
	}

	internal void AddLine(DialogLine line)
	{
		_lines.Add(line);
		var text = string.IsNullOrEmpty(line.Text) ? $"<{line.Unit}>" : line.Text;
		Log.Info($"Dialog {line.Speaker}: {text}");
	}

	internal void Finish()
	{
		_finished = true;
		_choices.Clear();
	}

	private void Enter(int index)
	{
		var choice = _choices[index];
		Except.Add(choice.Function);
		Log.Info($"Dialog choice: {(string.IsNullOrEmpty(choice.Text) ? "(important)" : choice.Text)}");

		var stop = _runtime.ExecuteInfo(_npc, choice);
		if (stop || _finished)
		{
			Finish();
			return;
		}

		// Sub-choices added by the information function take precedence; otherwise rebuild
		// the NPC menu without important entries, like DialogMenu::onDoneText.
		var sub = _runtime.TakeStoredChoices(choice.Info);
		if (sub.Count > 0)
		{
			SetChoices(sub);
			return;
		}

		SetChoices(_runtime.BuildChoices(this, false));
		if (_choices.Count == 0)
		{
			Finish();
		}
	}

	private void SetChoices(List<DialogChoice> choices)
	{
		_choices.Clear();
		_choices.AddRange(choices);
	}
}
