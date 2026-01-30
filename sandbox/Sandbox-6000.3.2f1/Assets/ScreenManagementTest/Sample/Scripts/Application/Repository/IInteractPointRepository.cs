using System.Collections.Generic;

namespace ScreenManagementSample.Application
{
    /// <summary>
    /// インタラクトポイントリポジトリインターフェース
    /// </summary>
    public interface IInteractPointRepository
    {
        /// <summary> 全てのインタラクトポイントを取得する </summary>
        IReadOnlyList<Domain.InteractPoint> GetAll();

        /// <summary> 指定位置のインタラクトポイントを取得する（なければnull） </summary>
        Domain.InteractPoint GetAt(Domain.Position position);

        /// <summary> インタラクトポイントをリセット（再生成）する </summary>
        void Reset();
    }
}
