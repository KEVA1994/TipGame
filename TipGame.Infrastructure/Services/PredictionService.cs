using TipGame.Domain.Entities;

public class PredictionService
{
    public void CalculatePoints(Match match)
    {
        foreach (var prediction in match.Predictions)
        {
            // Exact match → 3
            if (prediction.PredictedHome == match.HomeScore &&
                prediction.PredictedAway == match.AwayScore)
            {
                prediction.Points = 3;
            }
            // Draw → 2
            else if (prediction.PredictedHome == prediction.PredictedAway &&
                     match.HomeScore == match.AwayScore)
            {
                prediction.Points = 2;
            }
            // Winner → 1
            else if (
                (prediction.PredictedHome > prediction.PredictedAway && match.HomeScore > match.AwayScore) ||
                (prediction.PredictedHome < prediction.PredictedAway && match.HomeScore < match.AwayScore)
            )
            {
                prediction.Points = 1;
            }
            else
            {
                prediction.Points = 0;
            }
        }
    }
}