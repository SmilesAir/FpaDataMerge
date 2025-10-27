import { useState } from 'react'
import './App.css'

import allData from "C:/Github/FpaDataMerge/data/AllFrisbeeData-2025-10-7.json"
import playerMappings from "C:/Github/FpaDataMerge/data/playerMapping.json"
import fpaPlayers from "C:/Github/FpaDataMerge/data/fpaSqlPlayersWithCountry.json"
import newEvents from "C:/Github/FpaDataMerge/data/newEventSummaries.json"
import newResults from "C:/Github/FpaDataMerge/data/newResults.json"

const countryCodes = ["AFG", "ALA", "ALB", "DZA", "ASM", "AND", "AGO", "AIA", "ATA", "ATG", "ARG", "ARM", "ABW", "AUS", "AUT", "AZE", "BHS", "BHR", "BGD", "BRB", "BLR", "BEL", "BLZ", "BEN", "BMU", "BTN", "BOL", "BES", "BIH", "BWA", "BVT", "BRA", "IOT", "BRN", "BGR", "BFA", "BDI", "KHM", "CMR", "CAN", "CPV", "CYM", "CAF", "TCD", "CHL", "CHN", "CXR", "CCK", "COL", "COM", "COG", "COD", "COK", "CRI", "CIV", "HRV", "CUB", "CUW", "CYP", "CZE", "DNK", "DJI", "DMA", "DOM", "ECU", "EGY", "SLV", "GNQ", "ERI", "EST", "ETH", "FLK", "FRO", "FJI", "FIN", "FRA", "GUF", "PYF", "ATF", "GAB", "GMB", "GEO", "DEU", "GHA", "GIB", "GRC", "GRL", "GRD", "GLP", "GUM", "GTM", "GGY", "GIN", "GNB", "GUY", "HTI", "HMD", "VAT", "HND", "HKG", "HUN", "ISL", "IND", "IDN", "IRN", "IRQ", "IRL", "IMN", "ISR", "ITA", "JAM", "JPN", "JEY", "JOR", "KAZ", "KEN", "KIR", "PRK", "KOR", "XKX", "KWT", "KGZ", "LAO", "LVA", "LBN", "LSO", "LBR", "LBY", "LIE", "LTU", "LUX", "MAC", "MKD", "MDG", "MWI", "MYS", "MDV", "MLI", "MLT", "MHL", "MTQ", "MRT", "MUS", "MYT", "MEX", "FSM", "MDA", "MCO", "MNG", "MNE", "MSR", "MAR", "MOZ", "MMR", "NAM", "NRU", "NPL", "NLD", "NCL", "NZL", "NIC", "NER", "NGA", "NIU", "NFK", "MNP", "NOR", "OMN", "PAK", "PLW", "PSE", "PAN", "PNG", "PRY", "PER", "PHL", "PCN", "POL", "PRT", "PRI", "QAT", "SRB", "REU", "ROU", "RUS", "RWA", "BLM", "SHN", "KNA", "LCA", "MAF", "SPM", "VCT", "WSM", "SMR", "STP", "SAU", "SEN", "SYC", "SLE", "SGP", "SXM", "SVK", "SVN", "SLB", "SOM", "ZAF", "SGS", "SSD", "ESP", "LKA", "SDN", "SUR", "SJM", "SWZ", "SWE", "CHE", "SYR", "TWN", "TJK", "TZA", "THA", "TLS", "TGO", "TKL", "TON", "TTO", "TUN", "TUR", "XTX", "TKM", "TCA", "TUV", "UGA", "UKR", "ARE", "GBR", "USA", "UMI", "URY", "UZB", "VUT", "VEN", "VNM", "VGB", "VIR", "WLF", "ESH", "YEM", "ZMB", "ZWE"]
const awsPath = "https://v869a98rf9.execute-api.us-west-2.amazonaws.com/production/"

if (import.meta.hot) {
  import.meta.hot.dispose(() => {
    console.clear();
  });
}

let originalCount = 0
for (let playerdata of Object.values(allData.playersData)) {
  if (playerdata.aliasKey === undefined) {
    ++originalCount
  }
}
console.log(originalCount)

var once = false

function makeDateFromString(dateStr) {
  let date = new Date(dateStr)
  return date.toISOString().split("T")[0]
}

function convertToResultsData(eventKey, divisionName, inputStr) {
    return getData(`${awsPath}convertToResultsData/${eventKey}/divisionName/${divisionName}`, inputStr).then((data) => {
        if (data.success) {
            return data.resultsData
        } else {
            throw data.error
        }
    }).catch((error) => {
        console.error(`Failed to convert results: ${error}`)
    })
}

function parsePools(lines) {
    let resultsData = {}

    for (let i = 1; i < lines.length; ++i) {
        let line = lines[i]
        if (line.includes("round")) {
            let roundData = {
                id: parseInt(line.replace("round", "").trim(), 10)
            }

            let poolData = undefined
            for (++i; i < lines.length; ++i) {
                line = lines[i]
                if (line.includes("end") || line.includes("round")) {
                    if (poolData !== undefined) {
                        --i
                    }
                    break
                }

                if (line.includes("pool")) {
                    poolData = {
                        poolId: line.replace("pool", "").trim(),
                        teamData: []
                    }

                    for (++i; i < lines.length; ++i) {
                        line = lines[i]
                        if (line.includes("end") || line.includes("round") || line.includes("pool")) {
                            roundData[`pool${poolData.poolId}`] = poolData
                            --i
                            break
                        }

                        if (line.length > 0) {
                            let teamParts = line.split(" ")
                            let teamData = {
                                place: parseInt(teamParts[0], 10),
                                points: parseFloat(teamParts[teamParts.length - 1]) || 0
                            }
                            teamData.players = []
                            for (let partIndex = 1; partIndex < teamParts.length - 1; ++partIndex) {
                                teamData.players.push(teamParts[partIndex])
                            }
                            poolData.teamData.push(teamData)
                        }
                    }
                }
            }

            resultsData[`round${roundData.id}`] = roundData
        }
    }

    return resultsData
}

function App() {

  if (!once) {
    once = true

    console.log(allData)

    let playersOutput = ""
    for (let playerKey in allData.playersData) {
      let playerData = allData.playersData[playerKey]
      playersOutput += `${playerKey},${playerData.firstName},${playerData.lastName},${playerData.gender}\n`
    }

    //console.log(playersOutput)

    let eventsOutput = ""
    for (let eventKey in allData.eventsData) {
      let eventData = allData.eventsData[eventKey]
      eventsOutput += `${eventKey},${eventData.startDate},${eventData.endDate},${eventData.eventName}\n`
    }

    //console.log(eventsOutput)

    // let offsetTime = 0
    // for (let newEvent of newEvents.events) {
    //   setTimeout(() => {
    //     uploadEvent(newEvent.ryanId, newEvent.name, makeDateFromString(newEvent.start), makeDateFromString(newEvent.end),
    //       {
    //         fpaId: newEvent.id,
    //         postName: newEvent.postName
    //       })
    //   }, offsetTime)

    //   offsetTime += 100
    // }

    let offsetTime = 0
    for (let result of newResults.results) {
      setTimeout(() => {
        let markupStr = decodeURI(result.input)
        convertToResultsData(result.id, result.division, decodeURI(result.input)).then((resp) => {
          postData(`${awsPath}setEventResults/${result.id}/divisionName/${result.division}`, {
              resultsData: resp,
              rawText: markupStr,
              eventName: result.eventName
          }).then((response) => {
              console.log(response)
          }).catch((error) => {
              console.error(error)
              alert(`Error ${error}`)
          })
        })
      }, offsetTime)
      offsetTime += 100
    }

    //console.log(newResults)
  }

  return (
    <div>
      Js Helper
    </div>
  )
}

function getfpaPlayerByFpaId(fpaId) {
  return fpaPlayers.find((x) => `${x.player_id}` === fpaId)
}

function uploadFpaPlayers() {
  let timerMs = 0
  let incrementMs = 100
  // for (let exact of playerMappings.exacts) {
  //   let fpaPlayer = getfpaPlayerByFpaId(exact.fpaId)
  //   if (fpaPlayer) {
  //     setTimeout(() => {
  //       uploadPlayer(fpaPlayer.first_name, fpaPlayer.last_name, fpaPlayer.country, fpaPlayer.sex, exact.ryanId, exact.fpaId)
  //     }, timerMs += incrementMs)

  //     console.log(timerMs)
  //   }
  // }

  // for (let manual of playerMappings.manuals) {
  //   let fpaPlayer = getfpaPlayerByFpaId(manual.fpaId)
  //   if (fpaPlayer) {
  //     setTimeout(() => {
  //       uploadPlayer(fpaPlayer.first_name, fpaPlayer.last_name, fpaPlayer.country, fpaPlayer.sex, manual.ryanId, manual.fpaId)
  //     }, timerMs += incrementMs)
  //   }
  // }

  // for (let ignore of playerMappings.ignores) {
  //   let fpaPlayer = getfpaPlayerByFpaId(ignore.fpaId)
  //   if (fpaPlayer) {
  //     setTimeout(() => {
  //       uploadPlayer(fpaPlayer.first_name, fpaPlayer.last_name, fpaPlayer.country, fpaPlayer.sex, undefined, ignore.fpaId)
  //     }, timerMs += incrementMs)
  //   }
  // }
}

function getCountryCode(str) {
  switch (str) {
    case "Belgium":
      return "BEL"
    case "Canada":
      return "CAN"
    case "Czech Republic":
      return "CZE"
    case "Denmark":
      return "DNK"
    case "France":
      return "FRA"
    case "Germany":
      return "DEU"
    case "Israel":
      return "ISR"
    case "Italy":
      return "ITA"
    case "Japan":
      return "JPN"
    case "Netherlands":
      return "NLD"
    case "Norway":
      return "NOR"
    case "Russia":
      return "RUS"
    case "Slovakia":
      return "SVK"
    case "Sweden":
      return "SWE"
    case "Switzerland":
      return "CHE"
    case "UK":
      return "GBR"
    case "USA":
      return "USA"
    default:
      return ""
  }
}

function uploadPlayer(firstName, lastName, country, gender, aliasKey, fpaId) {
  let countryCode = getCountryCode(country)
  //console.log(firstName, lastName, countryCode, gender, aliasKey, fpaId)

  postData(`${awsPath}addPlayer/${firstName}/lastName/${lastName}`, {
      membership: 0,
      country: countryCode,
      gender: gender,
      aliasKey: aliasKey,
      fpaWebsiteId: fpaId
  }).then((response) => {
      console.log("succes", response)
  }).catch((error) => {
      console.error(error)
  })
}

function uploadEvent(eventKey, eventName, eventStartDate, eventEndDate, additionalData) {
  postData(`${awsPath}setEventSummary/${eventKey}`, {
      eventName: eventName,
      startDate: eventStartDate,
      endDate: eventEndDate,
      additionalData: additionalData
  }).then((response) => {
      console.log(response)
  }).catch((error) => {
      console.error(error)
  })
}

function postData(url, data) {
    return fetch(url, {
        method: "POST",
        mode: "cors",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(data)
    }).then((response) => {
        return response.json()
    })
}

function getData(url, data) {
    return fetch(url, {
        method: "POST",
        mode: "cors",
        headers: {
            "Content-Type": "application/json"
        },
        body: data
    }).then((response) => {
        return response.json()
    })
}

export default App
