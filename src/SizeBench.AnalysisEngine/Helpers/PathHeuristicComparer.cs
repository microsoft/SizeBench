using System.IO;

namespace SizeBench.AnalysisEngine.Helpers;

internal static class StringSimilarity
{
    /// <summary>
    /// Returns the number of steps required to transform the source string
    /// into the target string.
    /// </summary>
    public static int ComputeLevenshteinDistance(string source, string target)
    {
        var sourceWordCount = source.Length;
        var targetWordCount = target.Length;
        var distance = new int[sourceWordCount + 1, targetWordCount + 1];

        // Step 2
        for (var i = 0; i <= sourceWordCount; distance[i, 0] = i++)
        {
            ;
        }

        for (var j = 0; j <= targetWordCount; distance[0, j] = j++)
        {
            ;
        }

        for (var i = 1; i <= sourceWordCount; i++)
        {
            for (var j = 1; j <= targetWordCount; j++)
            {
                // Step 3
                var cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                // Step 4
                distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
            }
        }

        return distance[sourceWordCount, targetWordCount];
    }

    /// <summary>
    /// Calculate percentage similarity of two strings
    /// <param name="source">Source String to Compare with</param>
    /// <param name="target">Targeted String to Compare</param>
    /// <returns>Return Similarity between two strings from 0 to 1.0</returns>
    /// </summary>
    public static double CalculateSimilarityPercentage(string source, string target)
    {
        if ((source is null) || (target is null))
        {
            return 0.0;
        }

        if ((source.Length == 0) || (target.Length == 0))
        {
            return 0.0;
        }

        if (source == target)
        {
            return 1.0;
        }

        var stepsToSame = ComputeLevenshteinDistance(source, target);
        return (1.0 - (stepsToSame / (double)Math.Max(source.Length, target.Length)));
    }
}


internal static class PathHeuristicComparer
{
    public static bool PathNamesAreVerySimilar(string firstName, string secondName)
    {
        // When doing a diff, it's very important that we match with a pretty strong likelihood that the object (lib or compiland, generally)
        // is the same in 'before' and 'after'.  The best tool we have to go on is the Name, and most people don't change the name of a lib
        // or compiland between builds so this is "good enough."
        // One catch though is that we don't know where the enlistment root is, so we need to guess a little bit - if someone
        // builds the same binary from "r:\foo" and "p:\os" we don't want to treat each lib as different because the Name
        // will, strictly speaking, differ with this prefix.
        // So we'll compare the filename for sure, that must match to make any sense - but after that we'll go with a heuristic guess
        // that if 80% of the characters in the path are the same (or more) we'll consider it "the same for this purpose"
        //
        // Calculating this is fairly challenging since we kind of want two different things.  We want these two things to be considered
        // "similar enough":
        // p:\os\src\folder1\folder2\foo.obj
        // w:\dd\root2\src\folder1\folder2\foo.obj
        //
        // And we also want these two to be considered "similar enough" even though the part closest to the filename differs - the "enlistment root"
        // is essentially the entire folder path.
        // c:\foo\bar\baz\before\foo.lib
        // c:\foo\bar\baz\after\foo.lib
        // 
        // These require two different ways of considering "similarity" - the first we'll use a reverse-comparison loop, the second we'll use
        // Levenshtein Distance.  There are tests that'll end up checking both of these so feel free to fiddle with this heuristic further, it's
        // not perfect now.

        var firstFilename = Path.GetFileName(firstName.AsSpan());
        var secondFilename = Path.GetFileName(secondName.AsSpan());

        if (!firstFilename.Equals(secondFilename, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

#pragma warning disable CA1308 // Normalize strings to uppercase - these are file paths, lower invariant is ok
        firstName = firstName.ToLowerInvariant();
        secondName = secondName.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

        int charactersSame = firstFilename.Length, charactersDifferent = 0;
        var secondLength = secondName.Length - 1 - charactersSame;
        var firstLength = firstName.Length - 1 - charactersSame;
        while (secondLength > 0 && firstLength > 0)
        {
            if (secondName[secondLength] == firstName[firstLength])
            {
                charactersSame++;
            }
            else
            {
                charactersDifferent++;
            }

            firstLength--;
            secondLength--;
        }

        return (charactersSame / (float)(charactersSame + charactersDifferent)) >= 0.8 ||
               StringSimilarity.CalculateSimilarityPercentage(firstName, secondName) >= 0.85;
    }
}
