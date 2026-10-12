namespace StlExplorerClient
{
    /// <summary>
    /// Fond de l'aperçu : grille fine façon plateau d'impression, avec des repères
    /// d'angle en ambre.
    /// </summary>
    public class PlateauDrawable : IDrawable
    {
        private const float Pas = 40f;
        private const float Repere = 18f;
        private const float Retrait = 12f;

        public void Draw(ICanvas canvas, RectF zone)
        {
            canvas.SaveState();

            canvas.StrokeSize = 1;
            canvas.StrokeColor = Color.FromArgb("#262A28");

            // Grille centrée : les lignes sont symétriques par rapport au milieu du plateau.
            float cx = zone.Width / 2f, cy = zone.Height / 2f;
            for (float x = cx % Pas; x < zone.Width; x += Pas)
                canvas.DrawLine(x, 0, x, zone.Height);
            for (float y = cy % Pas; y < zone.Height; y += Pas)
                canvas.DrawLine(0, y, zone.Width, y);

            // Repères d'angle
            canvas.StrokeSize = 2;
            canvas.StrokeColor = Color.FromArgb("#E3A23B");
            float g = Retrait, d = zone.Width - Retrait, h = Retrait, b = zone.Height - Retrait;
            canvas.DrawLine(g, h, g + Repere, h); canvas.DrawLine(g, h, g, h + Repere);
            canvas.DrawLine(d, h, d - Repere, h); canvas.DrawLine(d, h, d, h + Repere);
            canvas.DrawLine(g, b, g + Repere, b); canvas.DrawLine(g, b, g, b - Repere);
            canvas.DrawLine(d, b, d - Repere, b); canvas.DrawLine(d, b, d, b - Repere);

            canvas.RestoreState();
        }
    }
}
