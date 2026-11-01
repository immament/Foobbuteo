// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Globalization;

[Serializable]
internal sealed class BuildVersionState
{
    private const string DateFormat = "yyyy-MM-dd";

    public int major;
    public string lastBuildDate;
    public int dailyBuildIndex;
    public int androidVersionCode;

    internal string Version
    {
        get
        {
            var date = DateTime.ParseExact(lastBuildDate, DateFormat, CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1:yy.MMdd}.{2}", major, date, dailyBuildIndex);
        }
    }

    internal void Reserve(DateTime today, bool isAndroid, int currentAndroidVersionCode)
    {
        if (major < 0 || dailyBuildIndex < 0 || androidVersionCode < 0)
            throw new ArgumentException("Build version numbers must not be negative.");
        if (
            !DateTime.TryParseExact(
                lastBuildDate,
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var previousDate
            )
        )
            throw new ArgumentException("lastBuildDate must use yyyy-MM-dd format.");
        if (today.Date < previousDate)
            throw new ArgumentException("The build date is earlier than lastBuildDate. Check the computer's date.");

        dailyBuildIndex = today.Date == previousDate ? checked(dailyBuildIndex + 1) : 1;
        lastBuildDate = today.ToString(DateFormat, CultureInfo.InvariantCulture);
        if (isAndroid)
            androidVersionCode = checked(Math.Max(androidVersionCode, currentAndroidVersionCode) + 1);
    }
}
