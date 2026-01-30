using System;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement.Dialog;

namespace Lilja.ScreenManagement.Mock
{
    public class TestDialog : SimpleDialogBase<ValueTuple, bool>
    {
        public int Version;

        /// <summary>
        /// Backdrop クリックで閉じる
        /// </summary>
        protected override bool EnableOutsideButton => true;

        /// <summary>
        /// Backdrop クリック時は false を返す
        /// </summary>
        protected override bool OutsideButtonResult => false;

        /// <inheritdoc/>
        protected override void Build()
        {
            Frame.SetTitle($"Version {Version}");
            Content.AddText("Body");
            Frame.AddButton("Back", () => Close(false));
            Frame.AddButton("Go", () => OnClickGoAsync().Forget());
            Frame.AddButton("Battle", () => new MockBattleOverlay().CallAsync(default, DisposeCancellationToken).Forget());
            Frame.AddButton("Character", () => new MockMenuCharacterOverlay().CallAsync(default, DisposeCancellationToken).Forget());
            Frame.AddButton("Title", () => World.Switch(typeof(MockTitleWorld), new ValueTuple()));
        }

        private async UniTask OnClickGoAsync()
        {
            var result = await new TestDialog { Version = Version + 1 }.CallAsync(default, DisposeCancellationToken);
            if (result)
            {
                Close(true);
            }
        }
    }
}
