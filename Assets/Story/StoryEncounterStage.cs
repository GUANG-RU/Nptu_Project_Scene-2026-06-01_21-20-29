using System;
using UnityEngine;

[Serializable]
public class StoryEncounterStage
{
    [Tooltip("階段標籤 (First / Second / Third)")]
    public string stageName = "First";

    [Header("此階段播放的對話文字與對應配音")]
    [TextArea(2, 5)]
    public string[] messages;

    [Tooltip("對應上述字幕的 AI 配音檔 (數量與順序須與 Messages 一致)")]
    public AudioClip[] voiceClips;
}