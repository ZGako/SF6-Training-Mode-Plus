local UniqueCharData = {}

UniqueCharData[1] = {
    name = "Ryu",
    timers = {
        {
            id = "timer_0_001",
            name = "Denjin Charge",
            -- if install, then we have a timerMaxValue, otherwise no. Also determines the descriptors
            install = false
            -- for timers, we only have values descriptors of
            --[[
            0 = "Standard",
            1 = "Maximum" (and "Activated" for Ryu only),
            2 = "Infinite"

            No reason to bother inserting it here, just do it programmatically later
        ]]
        }
    }
}

UniqueCharData[21] = {
    name = "Jamie",
    timers = {
        {
            id = "timer_0_021",
            name = "The Devil's Song",
            install = true,
            timerMaxValue = 900
        }
    },
    stocks = {
        {
            id = "stock_0_021",
            name = "Drink Level",
            maxValue = 4,
            minValue = 0,
            allowInfinite = false,
            correspond = true,
            descriptors = {
                "0",
                "1",
                "2",
                "3",
                "4"
            }
        }
    }
}

UniqueCharData[3] = {
    name = "Kimberly",
    stocks = {
        {
            id = "stock_0_003",
            name = "Shuriken Bomb Stocks",
            maxValue = 2,
            minValue = 0,
            allowInfinite = true,
            correspond = false,
            descriptors = {
                "2",
                "1",
                "0"
            }
        }
    }
}

UniqueCharData[12] = {
    name = "Lily",
    stocks = {
        {
            id = "stock_0_012",
            name = "Windclad Stocks",
            maxValue = 3,
            minValue = 0,
            allowInfinite = true,
            correspond = true,
            descriptors = {
                "0",
                "1",
                "2",
                "3"
            }
        }
    }
}

UniqueCharData[16] = {
    name = "Juri",
    timers = {
        {
            id = "timer_0_016",
            name = "Feng Shui Engine",
            install = true,
            timerMaxValue = 600
        }
    },
    stocks = {
        {
            id = "stock_0_016",
            name = "Fuha Stocks",
            maxValue = 3,
            minValue = 0,
            allowInfinite = true,
            correspond = true,
            descriptors = {
                "0",
                "1",
                "2",
                "3"
            }
        }
    }
}

UniqueCharData[20] = {
    name = "E.Honda",
    stocks = {
        {
            id = "stock_0_020",
            name = "Sumo Spirit",
            maxValue = 1,
            minValue = 0,
            allowInfinite = true,
            correspond = false,
            descriptors = {
                "Standard",
                "Activated"
            }
        }
    }
}

UniqueCharData[15] = {
    name = "Blanka",
    timers = {
        {
            id = "timer_0_015",
            name = "Lightning Beast",
            install = true,
            timerMaxValue = 1500
        }
    },
    stocks = {
        {
            id = "stock_0_015",
            name = "Blanka-Chan Bomb",
            maxValue = 3,
            minValue = 0,
            allowInfinite = true,
            correspond = false,
            descriptors = {
                "3",
                "2",
                "1",
                "0"
            }
        }
    }
}

UniqueCharData[18] = {
    name = "Guile",
    timers = {
        {
            id = "timer_0_018",
            name = "Solid Puncher",
            install = true,
            timerMaxValue = 1500
        }
    }
}

UniqueCharData[28] = {
    name = "Mai",
    stocks = {
        {
            id = "stock_0_028",
            name = "Flame Stocks",
            maxValue = 5,
            minValue = 0,
            allowInfinite = true,
            correspond = true,
            descriptors = {
                "0",
                "1",
                "2",
                "3",
                "4",
                "5"
            }
        }
    }
}

UniqueCharData[30] = {
    name = "C.Viper",
    timers = {
        {
            id = "timer_0_030",
            name = "Limit Decoupler",
            install = true,
            timerMaxValue = 700
        }
    }
}

UniqueCharData[5] = {
    name = "Manon",
    stocks = {
        {
            id = "stock_0_005",
            name = "Medal Level",
            maxValue = 4,
            minValue = 0,
            allowInfinite = false,
            correspond = false,
            descriptors = {
                "1",
                "2",
                "3",
                "4",
                "5"
            }
        }
    }
}

-- iterate through the char data and add timer descriptors
for _, charData in pairs(UniqueCharData) do
    if charData.timers then
        for _, timerData in pairs(charData.timers) do
            if not timerData.install then
                timerData.descriptors = {
                    "Standard",
                    "Activated",
                    "Infinite"
                }
            else
                timerData.descriptors = {
                    "Standard",
                    "Maximum",
                    "Infinite"
                }
            end
        end
    end
end

return UniqueCharData
