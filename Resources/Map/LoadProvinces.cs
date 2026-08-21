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
    private const int MaximumProvincesPerRegion = 5;
    private const int InitialRegionTarget = 10;
    public List<Province> provincelist;
    
    public void LoadStuff()
    {



        LoadinProvinces();
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
            if(newprovince.cultures.Count == 0)
            {
                culture.ownerIdentity = newprovince.nation.ownerIdentity;
                culture.population = 1000;
                culture.name = newprovince.nation.name;
                newprovince.cultures.Add(culture);
            }
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
                provincelist = members
            };
            foreach (Province province in members) province.region = regionName;
            Owners.Instance.regiondict.Add(regionName, region);
            Owners.Instance.regionlist.Add(region);
            regionsRemaining--;
        }
        SplitOversizedRegions();
        AttachLooseRegions();
        BalanceTwoProvinceRegions();
    }

    void SplitOversizedRegions()
    {
        foreach (CampaignRegion oversized in Owners.Instance.regionlist
            .Where(region => region.provincelist.Count > MaximumProvincesPerRegion).ToList())
        {
            List<Province> bestFirst = null;
            List<Province> members = oversized.provincelist;
            float bestScore = float.MaxValue;
            for (int a = 0; a < members.Count - 2; a++)
                for (int b = a + 1; b < members.Count - 1; b++)
                    for (int c = b + 1; c < members.Count; c++)
                    {
                        List<Province> first = new List<Province> { members[a], members[b], members[c] };
                        List<Province> second = members.Where(province => !first.Contains(province)).ToList();
                        if (!ProvincesAreConnected(first) || !ProvincesAreConnected(second) ||
                            second.Count > MaximumProvincesPerRegion) continue;
                        float score = ProvinceGroupShapeScore(first) + ProvinceGroupShapeScore(second);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestFirst = first;
                        }
                    }
            if (bestFirst == null && members.Count == 9)
                for (int a = 0; a < members.Count - 3; a++)
                    for (int b = a + 1; b < members.Count - 2; b++)
                        for (int c = b + 1; c < members.Count - 1; c++)
                            for (int d = c + 1; d < members.Count; d++)
                            {
                                List<Province> first = new List<Province> { members[a], members[b], members[c], members[d] };
                                List<Province> second = members.Where(province => !first.Contains(province)).ToList();
                                if (!ProvincesAreConnected(first) || !ProvincesAreConnected(second)) continue;
                                float score = ProvinceGroupShapeScore(first) + ProvinceGroupShapeScore(second);
                                if (score < bestScore)
                                {
                                    bestScore = score;
                                    bestFirst = first;
                                }
                            }
            if (bestFirst == null && members.Count == 10)
                for (int a = 0; a < members.Count - 4; a++)
                    for (int b = a + 1; b < members.Count - 3; b++)
                        for (int c = b + 1; c < members.Count - 2; c++)
                            for (int d = c + 1; d < members.Count - 1; d++)
                                for (int e = d + 1; e < members.Count; e++)
                                {
                                    List<Province> first = new List<Province>
                                        { members[a], members[b], members[c], members[d], members[e] };
                                    List<Province> second = members.Where(province => !first.Contains(province)).ToList();
                                    if (!ProvincesAreConnected(first) || !ProvincesAreConnected(second)) continue;
                                    float score = ProvinceGroupShapeScore(first) + ProvinceGroupShapeScore(second);
                                    if (score < bestScore)
                                    {
                                        bestScore = score;
                                        bestFirst = first;
                                    }
                                }
            if (bestFirst == null)
            {
                while (oversized.provincelist.Count > MaximumProvincesPerRegion)
                {
                    Province detachable = oversized.provincelist.FirstOrDefault(province =>
                        ProvincesAreConnected(oversized.provincelist.Where(other => other != province).ToList()));
                    if (detachable == null) break;
                    oversized.provincelist.Remove(detachable);
                    string fallbackName = UniqueRegionName(oversized.name + " Split");
                    CampaignRegion fallback = new CampaignRegion
                    {
                        name = fallbackName,
                        identity = RegionColor(Owners.Instance.regionlist.Count),
                        provincelist = new List<Province> { detachable }
                    };
                    detachable.region = fallbackName;
                    Owners.Instance.regiondict.Add(fallbackName, fallback);
                    Owners.Instance.regionlist.Add(fallback);
                }
                continue;
            }

            List<Province> bestSecond = members.Where(province => !bestFirst.Contains(province)).ToList();
            oversized.provincelist = bestFirst;
            foreach (Province province in bestFirst) province.region = oversized.name;

            string splitName = UniqueRegionName(oversized.name + " II");
            CampaignRegion split = new CampaignRegion
            {
                name = splitName,
                identity = RegionColor(Owners.Instance.regionlist.Count),
                provincelist = bestSecond
            };
            foreach (Province province in bestSecond) province.region = splitName;
            Owners.Instance.regiondict.Add(splitName, split);
            Owners.Instance.regionlist.Add(split);
        }
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
            foreach (CampaignRegion donor in Owners.Instance.regionlist.Where(region => region.provincelist.Count == 5))
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
