-- intellinsense
local re = re
local sdk = sdk
local reframework = reframework
local imgui = imgui

local module = {}

module.name = "SaveState Randomizer"
module.description = "Module for randomizing some parameters when loading save states."

module.data = {}
module.ui = {}

module.data.UniqueCharData = require("TrainingModePlusScripts/UniqueCharacterParametersData")

local Hotkey = require("Hotkeys/Hotkeys")
-- local ImguiPlus = require("_SharedCore/Imgui")

function module.init()
    -- only important thing is app.training.tf_OtherSettings.Load_SnapShot(app.training.tf_OtherSettings.LocalSnapShot) is the function we're hooking
end
