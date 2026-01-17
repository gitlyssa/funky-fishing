using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PondManager : MonoBehaviour
{
    // array of fish prefabs
    public GameObject[] fishPrefabs;
    // list of spawned fish
    public List<GameObject> fishList;

    public int radius = 30;
    public int waterlevel = 0;

    public GameObject playerBobber;
    Vector3 pondCenter;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pondCenter = transform.position;
        fishList = new List<GameObject>();
        // spawn 10 random fish at random position in pond
        for (int i = 0; i < 20; i++)
        {   

            // x^2 + z^2 < radius^2
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 randomPosition = new Vector3(pondCenter.x + randomCircle.x, 
            waterlevel + Random.Range(1, 5), 
            pondCenter.z + randomCircle.y);

            SpawnFish(-1, randomPosition);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // check for input key r to spawn random fish at random position in pond

        if (Input.GetKeyDown(KeyCode.R))
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 randomPosition = new Vector3(pondCenter.x + randomCircle.x, 
            waterlevel + Random.Range(1, 5), 
            pondCenter.z + randomCircle.y);

            SpawnFish(-1, randomPosition);
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (fishList.Count > 0)
            {
                int randomIndex = Random.Range(0, fishList.Count);
                RemoveFish(randomIndex);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            CatchFish(playerBobber);
        }
    }
    void SpawnFish(int fishIndex, Vector3 position)
    {
        if (fishIndex < 0)
        {
            // spawn random fish
            int randomIndex = Random.Range(0, fishPrefabs.Length);
            fishIndex = randomIndex;
        }
        GameObject fish = Instantiate(fishPrefabs[fishIndex], position, Quaternion.identity);
        //append to fish list
        fishList.Add(fish);
    }

    void RemoveFish(int fishIndex)
    {
        Destroy(fishList[fishIndex]);
        fishList.RemoveAt(fishIndex);
    }

    GameObject closestFish(GameObject bobber)
    {
        // find closest fish to bobber that intersects with bobber collider
        GameObject closestFish = null;

        Collider bobberCollider = bobber.GetComponent<Collider>();
        float closestDistance = Mathf.Infinity;
        foreach (GameObject fish in fishList)
        {
            Collider fishCollider = fish.GetComponent<Collider>();
            if (bobberCollider.bounds.Intersects(fishCollider.bounds))
            {
                float distance = Vector3.Distance(bobber.transform.position, fish.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestFish = fish;
                }
            }
        }
        return closestFish;
    }

    void CatchFish(GameObject bobber)
    {
        GameObject fish = closestFish(bobber);
        if (fish != null)
        {
            fishList.Remove(fish);
            Destroy(fish);
            Debug.Log("Fish caught!");
            playerBobber.GetComponent<BobberScript>().Reset();
        }
        else
        {
            Debug.Log("No fish caught.");
        }
    }
}
