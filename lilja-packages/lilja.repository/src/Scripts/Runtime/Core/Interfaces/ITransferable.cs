namespace Lilja.Repository
{
    /// <summary>
    /// Entity ↔ DTO変換のI/F。
    /// Source Generatorによって自動実装される。
    /// </summary>
    /// <typeparam name="TDto">DTOの型。</typeparam>
    public interface ITransferable<TDto>
    {
        /// <summary>
        /// EntityをDTOに変換する。
        /// </summary>
        /// <returns>変換されたDTO。</returns>
        TDto ToDto();

        /// <summary>
        /// DTOからEntityの状態を復元する。
        /// </summary>
        /// <param name="dto">復元元のDTO。</param>
        void FromDto(TDto dto);
    }
}
