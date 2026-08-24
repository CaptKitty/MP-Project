using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Linq;
using System.Text;

public class LoadProvinces : MonoBehaviour
{
    private const int MaximumProvincesPerRegion = 4;
    private const int InitialRegionTarget = 12;
    public List<Province> provincelist;
    
    public void LoadStuff()
    {



        LoadinProvinces();
        InitializeCulturalMixes();
        Owners.Instance.provincelist.Clear();
        Owners.Instance.provincelist = provincelist;
        AddStates();
        LoadProvincesinStates();
        BuildRegions();
    }

    public void LoadinCultures()
    {
        var txtarray = Resources.LoadAll("culturedatas", typeof(TextAsset));
        
        // using var sr = new StringReader(txt.text);
        int count = 0;  
        string Lines;
        int i = 0;
        foreach (TextAsset item in txtarray)
        {
            using var sr = new StringReader(item.text);
            string line = "potato";
            Culture culture = new Culture();
            while (line != null)
            {
                // Debug.Log(line);
                if(line.Contains("Name"))
                {
                    line = sr.ReadLine();
                    culture.name = line.Trim( );
                    // Debug.Log(provincename);
                }
                if(line.Contains("Color"))
                {
                    line = sr.ReadLine();
                    byte red = byte.Parse(line);
                    byte green = byte.Parse(sr.ReadLine());
                    byte blue = byte.Parse(sr.ReadLine());
                    culture.ownerIdentity = new Color32(red,green,blue,0);
                    // Debug.Log(color);
                }
                line = sr.ReadLine();
            }
            Owners.Instance.culturelist.Add(culture);
        }
    }

    void LoadinProvinces()
    {
        TextAsset txt = (TextAsset)Resources.Load("provincedata/Amsterdam", typeof(TextAsset));
        var txtarray = Resources.LoadAll("provincedatas", typeof(TextAsset));

        // var txtarray = new List<TextAsset>();
        // if(1==1)
        // {
        //     txtarray.Clear();
        //     var info = new DirectoryInfo(Application.streamingAssetsPath+ "/provincedatas");
        //     var fileInfo = info.GetFiles();
        //     foreach(FileInfo item in fileInfo)
        //     {
        //         if (item.Exists)
        //         {
        //             // Read the file contents
        //             string fileContent = File.ReadAllText(item.FullName);

        //             // Create a new TextAsset from the file contents
        //             TextAsset textAsset = new TextAsset(fileContent);
        //             txtarray.Add(textAsset);
        //         }   
        //     }
        // }
        
        
        // using var sr = new StringReader(txt.text);
        int count = 0;  
        string Lines;
        int i = 0;
        foreach (TextAsset item in txtarray)
        {
            // Debug.Log(item);
            using var sr = new StringReader(item.text);
            string line = "potato";

            string provincename = "Error";
            string regionname = string.Empty;
            Color32 color = new Color32(0,0,0,0);
            Vector2 location = new Vector2(0,0);
            int population = 1;
            Nation nation = new Nation();
            Province newprovince = new Province();
            newprovince.cultures = new List<Culture>();
            Culture culture = new Culture();
            while (line != null)
            {
                // Debug.Log(line);
                if(line.Contains("Name"))
                {
                    line = sr.ReadLine();
                    provincename = line.Trim( );
                    // Debug.Log(provincename);
                }
                if(line.Contains("Color"))
                {
                    line = sr.ReadLine();
                    byte red = byte.Parse(line);
                    byte green = byte.Parse(sr.ReadLine());
                    byte blue = byte.Parse(sr.ReadLine());
                    color = new Color32(red,green,blue,0);
                    // Debug.Log(color);
                }
                if(line.Contains("Location"))
                {
                    line = sr.ReadLine();
                    int x = int.Parse(line);
                    int y = int.Parse(sr.ReadLine());
                    location = new Vector2(x,y);
                    // Debug.Log(location);
                }
                // if(line.Contains("Population"))
                // {
                //     line = sr.ReadLine();
                //     population = int.Parse(line);
                //     culture = new Culture();
                //     culture.population = population;
                //     line = sr.ReadLine();
                //     if(line == "}")
                //     {
                //         //line = "Dutch";
                //         line = "None";
                //     }
                //     culture.name = line;
                //     culture.ownerIdentity = Owners.Instance.CallCultureByName(line).ownerIdentity;
                //     culture.name = Owners.Instance.CallCultureByName(line).name;
                //     newprovince.cultures.Add(culture);
                    
                //     // Debug.Log(population);
                // }
                if(line.Contains("Owner"))
                {
                    line = sr.ReadLine();
                    line = sr.ReadLine();
                    newprovince.nation = GetNation(line.Trim( ));
                    // Debug.Log(nation);
                }
                if(line.Contains("Region"))
                {
                    line = sr.ReadLine();
                    regionname = line == null ? string.Empty : line.Trim();
                }
                line = sr.ReadLine();
            }
            if(culture.name == "None")
            {
                if(newprovince.nation.name == "France")
                {
                    culture.name = "French";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
                if(newprovince.nation.name == "Spain")
                {
                    culture.name = "Spanish";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
                if(newprovince.nation.name == "Portugal")
                {
                    culture.name = "Portuguese";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
                if(newprovince.nation.name == "Netherlands")
                {
                    culture.name = "Dutch";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
            }
            
            newprovince.name = provincename;
            newprovince.region = string.IsNullOrWhiteSpace(regionname)
                ? DeriveRegionName(provincename)
                : regionname;
            newprovince.identity = color;
            newprovince.position = location;
            newprovince.EnsureCulture();
            newprovince.UpdatePopulation();
            // newprovince.population = population;
            
            // newprovince.nation = nation;

            provincelist.Add(newprovince);
        }        
    }

    void BuildRegions()
    {
        Owners.Instance.regionlist = new List<CampaignRegion>();
        Owners.Instance.regiondict = new Dictionary<string, CampaignRegion>(StringComparer.OrdinalIgnoreCase);

        List<Province> remaining = Owners.Instance.provincelist
            .Where(province => province != null)
            .OrderBy(province => province.name, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (Province province in remaining)
            if (string.IsNullOrWhiteSpace(province.region)) province.region = DeriveRegionName(province.name);

        int regionsRemaining = Mathf.CeilToInt(remaining.Count / (float)InitialRegionTarget);
        Dictionary<string, int> nameUses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (remaining.Count > 0)
        {
            int targetSize = Mathf.Clamp(Mathf.CeilToInt(remaining.Count / (float)Mathf.Max(1, regionsRemaining)), 3,
                InitialRegionTarget);
            targetSize = Mathf.Min(targetSize, remaining.Count);
            Province seed = ChooseRegionSeed(remaining);
            List<Province> members = new List<Province> { seed };
            remaining.Remove(seed);

            while (members.Count < targetSize && remaining.Count > 0)
            {
                Province next = remaining
                    .Where(candidate => IsAdjacentToRegion(candidate, members))
                    .OrderBy(candidate => RegionShapeScore(candidate, members))
                    .ThenByDescending(candidate => RemainingDegree(candidate, remaining))
                    .ThenBy(candidate => candidate.name, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (next == null) break;
                members.Add(next);
                remaining.Remove(next);
            }

            string baseName = string.IsNullOrWhiteSpace(seed.region) ? DeriveRegionName(seed.name) : seed.region;
            nameUses.TryGetValue(baseName, out int useCount);
            nameUses[baseName] = ++useCount;
            string regionName = useCount == 1 && !Owners.Instance.regiondict.ContainsKey(baseName)
                ? baseName : baseName + " (" + useCount + ")";
            while (Owners.Instance.regiondict.ContainsKey(regionName))
                regionName = baseName + " (" + (++useCount) + ")";
            nameUses[baseName] = useCount;

            CampaignRegion region = new CampaignRegion
            {
                name = regionName,
                identity = RegionColor(Owners.Instance.regionlist.Count),
                loyalty = 100f,
                provincelist = members
            };
            foreach (Nation owner in members.Where(province => province != null && province.nation != null)
                .Select(province => province.nation).Distinct()) region.SetLoyalty(owner, 100f);
            foreach (Province province in members) province.region = regionName;
            Owners.Instance.regiondict.Add(regionName, region);
            Owners.Instance.regionlist.Add(region);
            regionsRemaining--;
        }
        SplitOversizedRegions();
        AttachLooseRegions();
        BalanceTwoProvinceRegions();
        RenameRegionsFromCentralProvinces();
    }

    void RenameRegionsFromCentralProvinces()
    {
        Dictionary<CampaignRegion, string> baseNames = new Dictionary<CampaignRegion, string>();
        Dictionary<CampaignRegion, Vector2> centers = new Dictionary<CampaignRegion, Vector2>();
        foreach (CampaignRegion region in Owners.Instance.regionlist)
        {
            if (region == null || region.provincelist == null || region.provincelist.Count == 0) continue;
            Vector2 center = RegionCenter(region);
            Province centralProvince = region.provincelist
                .Where(province => province != null)
                .OrderBy(province => Vector2.SqrMagnitude(province.position - center))
                .ThenBy(province => province.name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            centers[region] = center;
            baseNames[region] = centralProvince != null ? DeriveRegionName(centralProvince.name) : "Unassigned";
        }

        Dictionary<CampaignRegion, string> finalNames = new Dictionary<CampaignRegion, string>();
        foreach (IGrouping<string, CampaignRegion> group in baseNames.Keys
            .GroupBy(region => baseNames[region], StringComparer.OrdinalIgnoreCase))
        {
            List<CampaignRegion> duplicates = group.ToList();
            if (duplicates.Count == 1)
            {
                finalNames[duplicates[0]] = group.Key;
                continue;
            }

            Vector2 groupCenter = Vector2.zero;
            foreach (CampaignRegion region in duplicates) groupCenter += centers[region];
            groupCenter /= duplicates.Count;

            if (duplicates.Count == 2)
            {
                CampaignRegion first = duplicates[0];
                CampaignRegion second = duplicates[1];
                Vector2 separation = centers[first] - centers[second];
                bool vertical = Mathf.Abs(separation.y) >= Mathf.Abs(separation.x);
                if (vertical)
                {
                    CampaignRegion north = centers[first].y >= centers[second].y ? first : second;
                    CampaignRegion south = north == first ? second : first;
                    finalNames[north] = "North " + group.Key;
                    finalNames[south] = "South " + group.Key;
                }
                else
                {
                    CampaignRegion east = centers[first].x >= centers[second].x ? first : second;
                    CampaignRegion west = east == first ? second : first;
                    finalNames[east] = "East " + group.Key;
                    finalNames[west] = "West " + group.Key;
                }
                continue;
            }

            foreach (CampaignRegion region in duplicates)
            {
                Vector2 offset = centers[region] - groupCenter;
                string direction = Mathf.Abs(offset.y) >= Mathf.Abs(offset.x)
                    ? (offset.y >= 0f ? "North " : "South ")
                    : (offset.x >= 0f ? "East " : "West ");
                finalNames[region] = direction + group.Key;
            }
        }

        Owners.Instance.regiondict = new Dictionary<string, CampaignRegion>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> collisions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (CampaignRegion region in Owners.Instance.regionlist)
        {
            if (region == null || !finalNames.TryGetValue(region, out string requestedName)) continue;
            collisions.TryGetValue(requestedName, out int count);
            collisions[requestedName] = ++count;
            string uniqueName = count == 1 ? requestedName : requestedName + " (" + count + ")";
            while (Owners.Instance.regiondict.ContainsKey(uniqueName))
                uniqueName = requestedName + " (" + (++count) + ")";
            collisions[requestedName] = count;

            region.name = uniqueName;
            foreach (Province province in region.provincelist)
                if (province != null) province.region = uniqueName;
            Owners.Instance.regiondict.Add(uniqueName, region);
        }
    }

    static Vector2 RegionCenter(CampaignRegion region)
    {
        Vector2 center = Vector2.zero;
        int count = 0;
        foreach (Province province in region.provincelist)
        {
            if (province == null) continue;
            center += province.position;
            count++;
        }
        return count > 0 ? center / count : Vector2.zero;
    }

    void SplitOversizedRegions()
    {
        foreach (CampaignRegion oversized in Owners.Instance.regionlist
            .Where(region => region.provincelist.Count > MaximumProvincesPerRegion).ToList())
        {
            int splitNumber = 2;
            while (oversized.provincelist.Count > MaximumProvincesPerRegion)
            {
                int groupsNeeded = Mathf.CeilToInt(oversized.provincelist.Count /
                    (float)MaximumProvincesPerRegion);
                int desiredSize = Mathf.CeilToInt(oversized.provincelist.Count / (float)groupsNeeded);
                List<Province> splitMembers = FindBestConnectedSubset(oversized.provincelist, desiredSize);
                if (splitMembers == null)
                {
                    Province detachable = oversized.provincelist.FirstOrDefault(province =>
                        ProvincesAreConnected(oversized.provincelist.Where(other => other != province).ToList()));
                    if (detachable == null) break;
                    splitMembers = new List<Province> { detachable };
                }
                foreach (Province province in splitMembers) oversized.provincelist.Remove(province);
                string splitName = UniqueRegionName(oversized.name + " Part " + splitNumber++);
                CampaignRegion split = new CampaignRegion
                {
                    name = splitName,
                    identity = RegionColor(Owners.Instance.regionlist.Count),
                    loyalty = 100f,
                    provincelist = splitMembers
                };
                foreach (Nation owner in splitMembers.Where(province => province != null && province.nation != null)
                    .Select(province => province.nation).Distinct()) split.SetLoyalty(owner, 100f);
                foreach (Province province in splitMembers) province.region = splitName;
                Owners.Instance.regiondict.Add(splitName, split);
                Owners.Instance.regionlist.Add(split);
            }
            foreach (Province province in oversized.provincelist) province.region = oversized.name;
        }
    }

    void InitializeCulturalMixes()
    {
        List<Province> provinces = provincelist.Where(province => province != null).ToList();
        Dictionary<Province, Culture> originalCultures = new Dictionary<Province, Culture>();
        foreach (Province province in provinces)
        {
            province.EnsureCulture();
            originalCultures[province] = province.PrimaryCulture;
        }

        foreach (Province province in provinces)
        {
            Culture primary = originalCultures[province];
            int totalPopulation = Mathf.Max(1, province.population);
            List<Culture> minorities = provinces.Where(other => other != province &&
                    originalCultures[other] != null && primary != null &&
                    !string.Equals(originalCultures[other].name, primary.name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(other => Vector2.Distance(province.position, other.position))
                .Select(other => originalCultures[other])
                .GroupBy(culture => culture.name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).Take(2).ToList();

            int firstMinority = minorities.Count > 0 ? Mathf.RoundToInt(totalPopulation * .15f) : 0;
            int secondMinority = minorities.Count > 1 ? Mathf.RoundToInt(totalPopulation * .05f) : 0;
            if (minorities.Count == 1) firstMinority = Mathf.RoundToInt(totalPopulation * .20f);
            province.cultures = new List<Culture>
            {
                new Culture
                {
                    name = primary != null ? primary.name : "Unassigned",
                    ownerIdentity = primary != null ? primary.ownerIdentity : province.identity,
                    population = totalPopulation - firstMinority - secondMinority
                }
            };
            if (minorities.Count > 0) province.cultures.Add(new Culture
            {
                name = minorities[0].name, ownerIdentity = minorities[0].ownerIdentity, population = firstMinority
            });
            if (minorities.Count > 1) province.cultures.Add(new Culture
            {
                name = minorities[1].name, ownerIdentity = minorities[1].ownerIdentity, population = secondMinority
            });
            province.UpdatePopulation();
        }
    }

    static List<Province> FindBestConnectedSubset(List<Province> members, int desiredSize)
    {
        if (desiredSize <= 0 || desiredSize >= members.Count || members.Count > 30) return null;
        List<Province> best = null;
        float bestScore = float.MaxValue;
        int limit = 1 << members.Count;
        for (int mask = 1; mask < limit; mask++)
        {
            int bits = 0;
            for (int value = mask; value != 0; value &= value - 1) bits++;
            if (bits != desiredSize) continue;
            List<Province> subset = new List<Province>();
            List<Province> remainder = new List<Province>();
            for (int index = 0; index < members.Count; index++)
                if ((mask & 1 << index) != 0) subset.Add(members[index]); else remainder.Add(members[index]);
            if (!ProvincesAreConnected(subset) || !ProvincesAreConnected(remainder)) continue;
            float score = ProvinceGroupShapeScore(subset) + ProvinceGroupShapeScore(remainder);
            if (score < bestScore) { bestScore = score; best = subset; }
        }
        return best;
    }

    string UniqueRegionName(string requestedName)
    {
        if (!Owners.Instance.regiondict.ContainsKey(requestedName)) return requestedName;
        int suffix = 2;
        string candidate;
        do candidate = requestedName + " (" + suffix++ + ")";
        while (Owners.Instance.regiondict.ContainsKey(candidate));
        return candidate;
    }

    static bool ProvincesAreConnected(List<Province> provinces)
    {
        if (provinces.Count <= 1) return true;
        HashSet<Province> visited = new HashSet<Province> { provinces[0] };
        Queue<Province> frontier = new Queue<Province>();
        frontier.Enqueue(provinces[0]);
        while (frontier.Count > 0)
        {
            Province current = frontier.Dequeue();
            foreach (Province province in provinces)
                if (!visited.Contains(province) && AreAdjacent(current, province))
                {
                    visited.Add(province);
                    frontier.Enqueue(province);
                }
        }
        return visited.Count == provinces.Count;
    }

    static float ProvinceGroupShapeScore(List<Province> provinces)
    {
        Vector2 centroid = Vector2.zero;
        foreach (Province province in provinces) centroid += province.position;
        centroid /= provinces.Count;
        float score = 0f;
        foreach (Province province in provinces) score += Vector2.SqrMagnitude(province.position - centroid);
        return score / provinces.Count;
    }

    void BalanceTwoProvinceRegions()
    {
        foreach (CampaignRegion receiver in Owners.Instance.regionlist
            .Where(region => region.provincelist.Count == 2)
            .OrderBy(region => region.name, StringComparer.OrdinalIgnoreCase).ToList())
        {
            Province bestProvince = null;
            CampaignRegion bestDonor = null;
            float bestScore = float.MaxValue;
            foreach (CampaignRegion donor in Owners.Instance.regionlist
                .Where(region => region.provincelist.Count == MaximumProvincesPerRegion))
            {
                foreach (Province candidate in donor.provincelist)
                {
                    if (!receiver.provincelist.Exists(member => AreAdjacent(candidate, member)) ||
                        !RegionRemainsConnectedWithout(donor, candidate)) continue;
                    float score = RegionShapeScore(candidate, receiver.provincelist);
                    if (score < bestScore || Mathf.Approximately(score, bestScore) &&
                        string.CompareOrdinal(candidate.name, bestProvince != null ? bestProvince.name : string.Empty) < 0)
                    {
                        bestScore = score;
                        bestProvince = candidate;
                        bestDonor = donor;
                    }
                }
            }
            if (bestProvince == null || bestDonor == null) continue;
            bestDonor.provincelist.Remove(bestProvince);
            receiver.provincelist.Add(bestProvince);
            bestProvince.region = receiver.name;
        }
    }

    static bool RegionRemainsConnectedWithout(CampaignRegion region, Province removed)
    {
        List<Province> remaining = region.provincelist.Where(province => province != removed).ToList();
        if (remaining.Count <= 1) return true;
        HashSet<Province> visited = new HashSet<Province>();
        Queue<Province> frontier = new Queue<Province>();
        frontier.Enqueue(remaining[0]);
        visited.Add(remaining[0]);
        while (frontier.Count > 0)
        {
            Province current = frontier.Dequeue();
            foreach (Province province in remaining)
                if (!visited.Contains(province) && AreAdjacent(current, province))
                {
                    visited.Add(province);
                    frontier.Enqueue(province);
                }
        }
        return visited.Count == remaining.Count;
    }

    void AttachLooseRegions()
    {
        bool merged;
        do
        {
            merged = false;
            List<CampaignRegion> looseRegions = Owners.Instance.regionlist
                .Where(region => region.provincelist.Count < 3)
                .OrderBy(region => region.provincelist.Count).ThenBy(region => region.name,
                    StringComparer.OrdinalIgnoreCase).ToList();
            foreach (CampaignRegion loose in looseRegions)
            {
                CampaignRegion target = Owners.Instance.regionlist
                    .Where(region => region != loose &&
                        region.provincelist.Count + loose.provincelist.Count <= MaximumProvincesPerRegion &&
                        RegionsAreAdjacent(loose, region))
                    .OrderBy(region => CombinedRegionShapeScore(loose, region))
                    .ThenBy(region => region.name, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (target == null) continue;

                foreach (Province province in loose.provincelist)
                {
                    province.region = target.name;
                    target.provincelist.Add(province);
                }
                Owners.Instance.regiondict.Remove(loose.name);
                Owners.Instance.regionlist.Remove(loose);
                merged = true;
                break;
            }
        } while (merged);
    }

    static bool RegionsAreAdjacent(CampaignRegion first, CampaignRegion second)
    {
        return first.provincelist.Exists(a => second.provincelist.Exists(b => AreAdjacent(a, b)));
    }

    static float CombinedRegionShapeScore(CampaignRegion first, CampaignRegion second)
    {
        List<Province> members = new List<Province>(first.provincelist);
        members.AddRange(second.provincelist);
        Vector2 centroid = Vector2.zero;
        foreach (Province province in members) centroid += province.position;
        centroid /= members.Count;
        float score = 0f;
        foreach (Province province in members) score += Vector2.SqrMagnitude(province.position - centroid);
        return score / members.Count;
    }

    static Province ChooseRegionSeed(List<Province> provinces)
    {
        return provinces.OrderByDescending(province => provinces.Count(other => other != province && AreAdjacent(province, other)))
            .ThenBy(province => province.name, StringComparer.OrdinalIgnoreCase).First();
    }

    static bool IsAdjacentToRegion(Province province, List<Province> region)
    {
        return region.Exists(member => AreAdjacent(province, member));
    }

    static bool AreAdjacent(Province first, Province second)
    {
        return first != null && second != null && Vector2.Distance(first.position, second.position) < 50f;
    }

    static int RemainingDegree(Province province, List<Province> remaining)
    {
        return remaining.Count(other => other != province && AreAdjacent(province, other));
    }

    static float RegionShapeScore(Province candidate, List<Province> members)
    {
        Vector2 centroid = candidate.position;
        foreach (Province member in members) centroid += member.position;
        centroid /= members.Count + 1;

        float spread = Vector2.SqrMagnitude(candidate.position - centroid);
        float farthest = spread;
        foreach (Province member in members)
        {
            float distance = Vector2.SqrMagnitude(member.position - centroid);
            spread += distance;
            farthest = Mathf.Max(farthest, distance);
        }

        // Low variance and a short outer radius favor rounded clusters over chains.
        return spread / (members.Count + 1) + farthest * 2f;
    }

    static Color32 RegionColor(int regionIndex)
    {
        // Golden-ratio hue spacing keeps neighboring generated regions visually distinct.
        float hue = Mathf.Repeat(regionIndex * 0.61803398875f, 1f);
        float saturation = 0.62f + 0.12f * (regionIndex % 2);
        float value = 0.78f + 0.12f * ((regionIndex / 2) % 2);
        return (Color32)Color.HSVToRGB(hue, saturation, value);
    }

    static string DeriveRegionName(string provinceName)
    {
        if (string.IsNullOrWhiteSpace(provinceName)) return "Unassigned";
        int separator = provinceName.LastIndexOf('_');
        if (separator > 0 && separator < provinceName.Length - 1 &&
            int.TryParse(provinceName.Substring(separator + 1), out _))
            return provinceName.Substring(0, separator).Trim();
        return provinceName.Trim();
    }
    void LoadProvincesinStates()
    {
  
        foreach (Province province in Owners.Instance.provincelist)
        {
            foreach (State state in Owners.Instance.statelist)
            {
                if(state.nation.name == province.nation.name)
                {
                    if(state.stateIdentity == new Color(0,0,0,0))
                    {
                        state.stateIdentity = province.identity;
                    }
                    state.provincelist.Add(province);
                    province.state = state.name;
                }
            }
        }
    }
    void AddStates()
    {
        foreach (Nation nation in Owners.Instance.nationlist)
        {
            State state = new State();
            state.name = nation.name;
            state.nation = nation;
            state.taxpercentage = 10;
            state.levypercentage = 10;
            state.stateIdentity = new Color32(0,0,0,0);
            state.provincelist = new List<Province>();
            Owners.Instance.statelist.Add(state);
        }
    }

    Nation GetNation(string name)
    {
        foreach (Nation nation in Owners.Instance.nationlist)
        {
            // Debug.Log(name + " + " + nation.name);
            if(name == nation.name)
            {
                // Debug.Log(nation.ownerIdentity);
                return nation;
            }
        }
        return new Nation();
    }

    void LoadBasePopulation()
    {
        
    }
}
