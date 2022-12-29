namespace MyGame.Utils;

/// <summary>
/// https://github.com/forrestthewoods/lib_fts/blob/master/code/fts_fuzzy_match.h
/// </summary>
public static class FuzzySearch
{
	public static bool fuzzy_match(string pattern, string str, out int outScore)
	{
		Span<int> matches = stackalloc int[256];
		return fuzzy_match(pattern, str, out outScore, matches);
	}

	public static bool fuzzy_match(string pattern, string str, out int outScore, Span<int> matches)
	{
		const int recursionCount = 0;
		const int recursionLimit = 10;

		return fuzzy_match_recursive(pattern, 0, str, 0, out outScore, Array.Empty<int>(), matches, matches.Length, 0,
			recursionCount, recursionLimit);
	}

	private static bool fuzzy_match_recursive(ReadOnlySpan<char> pattern, int patternCurIndex, ReadOnlySpan<char> str,
		int strCurIndex, out int outScore, Span<int> srcMatches, Span<int> matches, int maxMatches, int nextMatch,
		int recursionCount, int recursionLimit)
	{
		outScore = 0;
		// Count recursions
		++recursionCount;
		if (recursionCount >= recursionLimit)
			return false;

		// Detect end of strings
		if (patternCurIndex == pattern.Length || strCurIndex == str.Length)
			return false;

		// Recursion params
		var recursiveMatch = false;
		Span<int> bestRecursiveMatches = stackalloc int[256];
		Span<int> recursiveMatches = stackalloc int[256];
		var bestRecursiveScore = 0;

		// Loop through pattern and str looking for a match
		var firstMatch = true;
		while (patternCurIndex < pattern.Length && strCurIndex < str.Length)
		{
			// Found match
			if (char.ToLower(pattern[patternCurIndex]) == char.ToLower(str[strCurIndex]))
			{
				// Supplied matches buffer was too short
				if (nextMatch >= maxMatches)
					return false;

				// "Copy-on-Write" srcMatches into matches
				if (firstMatch && srcMatches.Length > 0)
				{
					srcMatches[..nextMatch].CopyTo(matches);
					firstMatch = false;
				}

				// Recursive call that "skips" this match
				if (fuzzy_match_recursive(pattern, patternCurIndex, str, strCurIndex + 1, out var recursiveScore,
					    matches, recursiveMatches, recursiveMatches.Length, nextMatch, recursionCount, recursionLimit))
				{
					// Pick best recursive score
					if (!recursiveMatch || recursiveScore > bestRecursiveScore)
					{
						recursiveMatches.CopyTo(bestRecursiveMatches);
						bestRecursiveScore = recursiveScore;
					}

					recursiveMatch = true;
				}

				// Advance
				matches[nextMatch++] = strCurIndex;
				++patternCurIndex;
			}

			++strCurIndex;
		}

		// Determine if full pattern was matched
		var matched = patternCurIndex == pattern.Length;

		// Calculate score
		if (matched)
		{
			const int sequential_bonus = 15; // bonus for adjacent matches
			const int separator_bonus = 30; // bonus if match occurs after a separator
			const int camel_bonus = 30; // bonus if match is uppercase and prev is lower
			const int first_letter_bonus = 15; // bonus if the first letter is matched

			const int leading_letter_penalty = -5; // penalty applied for every letter in str before the first match
			const int max_leading_letter_penalty = -15; // maximum penalty for leading letters
			const int unmatched_letter_penalty = -1; // penalty for every letter that doesn't matter

			// Initialize score
			outScore = 100;

			// Apply leading letter penalty
			var penalty = leading_letter_penalty * matches[0];
			if (penalty < max_leading_letter_penalty)
				penalty = max_leading_letter_penalty;
			outScore += penalty;

			// Apply unmatched penalty
			var unmatched = str.Length - nextMatch;
			outScore += unmatched_letter_penalty * unmatched;

			// Apply ordering bonuses
			for (var i = 0; i < nextMatch; ++i)
			{
				var currIdx = matches[i];

				if (i > 0)
				{
					var prevIdx = matches[i - 1];

					// Sequential
					if (currIdx == (prevIdx + 1))
						outScore += sequential_bonus;
				}

				// Check for bonuses based on neighbor character value
				if (currIdx > 0)
				{
					// Camel case
					var neighbor = str[currIdx - 1];
					var curr = str[currIdx];
					if (char.IsLower(neighbor) && char.IsUpper(curr))
						outScore += camel_bonus;

					// Separator
					var neighborSeparator = neighbor == '_' || neighbor == ' ';
					if (neighborSeparator)
						outScore += separator_bonus;
				}
				else
				{
					// First letter
					outScore += first_letter_bonus;
				}
			}
		}

		// Return best result
		if (recursiveMatch && (!matched || bestRecursiveScore > outScore))
		{
			// Recursive score is better than "this"
			bestRecursiveMatches.CopyTo(matches);
			outScore = bestRecursiveScore;
			return true;
		}

		if (matched)
		{
			// "this" score is better than recursive
			return true;
		}

		// no match
		return false;
	}
}
