namespace TvTimeViewer.Services;

public static class ImportRules
{
    public static readonly HashSet<string> SafeImportFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "tracking-prod-records.csv",
        "tracking-prod-records-v2.csv",
        "followedtvshow.csv",
        "usertvshowdata.csv",
        "userstatistics.csv",
        "userbadge.csv",
        "showseenepisodelatest.csv",
        "seenepisodelatest.csv",
        "seenepisodesource.csv",
        "showaddictionscore.csv",
        "usershowspecialstatus.csv",
        "episodecomment.csv",
        "showcomment.csv",
        "comments-prod-comments.csv",
        "emotions-3-prod-episodevotes.csv",
        "emotions-live-votes.csv",
        "episodeemotion.csv",
        "tvshowuseremotioncount.csv",
        "ratings-3-prod-episodevotes.csv",
        "ratings-live-votes.csv",
        "ratings-prod-episodevotes.csv",
        "ratings-v2-prod-votes.csv",
        "showcharacterepisodevote.csv",
        "stats-prod-cache.csv",
        "tracking-prod-count-by-timeframe.csv"
    };

    public static readonly HashSet<string> SensitiveFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "accesstoken.csv",
        "refreshtoken.csv",
        "auth-prod-login.csv",
        "ipaddress.csv",
        "adidentifier.csv",
        "devicetoken.csv",
        "devicedata.csv",
        "userdevice.csv",
        "usersession.csv",
        "useragent.csv",
        "user.csv",
        "userpersonaldata.csv",
        "userfacebookdata.csv",
        "usersocialdata.csv",
        "appsflyerids.csv",
        "webhookdata.csv",
        "installtracking.csv",
        "installedapp.csv"
    };
}