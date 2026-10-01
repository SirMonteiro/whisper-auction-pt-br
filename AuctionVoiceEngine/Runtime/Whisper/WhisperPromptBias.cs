namespace AuctionEngine.Whisper
{
    /// <summary>
    /// Initial prompt biasing text provided to Whisper.
    /// Conditions the ASR decoder attention heads towards auction slang, Brazilian fauna notes,
    /// bingo expressions, and numbers.
    /// </summary>
    public static class WhisperPromptBias
    {
        public const string DefaultPortuguesePrompt =
            "Lances e pregão de leilão: dobro, dobra, dobre, dobrar, duplico, duplicar, duplicarei, " +
            "triplico, triplicar, quadruplicar, quadreplico, mais, vezes, centavos, reais, barão, barões, " +
            "pau, paus, conto, pila, onça, garoupa, lobo guará, dois patinhos na lagoa, " +
            "idade de cristo, justiça de goiás, três oitão, pernas da xuxa, meia dúzia, duzentão, quinhentão.";
    }
}
