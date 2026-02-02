-- intellinsense
local re = re
local sdk = sdk
local reframework = reframework
local imgui = imgui

local module = {
    data = {}
}

module.name = "Training Settings + Randomizer"
module.description =
    "Module for adjusting training mode settings and randomizing them. Modifiable parameters include: Health, Drive, Super, Position, Unique Resources"

--[[
    We use the MVC pattern to separate data, logic, and UI.
    The module is what ties all the separate MVC compoenents together.

    The model contains the live game data.
    The controller has variables for logic and states that get saved to memory to reload during different insteances of training sessions.
    The view has variables for UI that can be freely discarted between sessions.
]]
--[[
    player specific parameters (grouped into one since they have very similar and simple logic)
]]
local PlayerParam = {
    model = {
        p1 = {},
        p2 = {}
    },
    view = {
        p1 = {},
        p2 = {}
    },
    controller = {
        p1 = {},
        p2 = {}
    }
}

function PlayerParam:init_player(PlayerIndex, PlayerParams)
    PlayerView = self.view[PlayerIndex]
    PlayerController = self.controller[PlayerIndex]

    -- copy the model
    self.model[PlayerIndex] = PlayerParams
    PlayerParams.Is_DG_Point_Lock = true

    --[[
        Health parameter initialization
    ]]
    -- define the controller variables
    PlayerController.health_randomizer = {}
    PlayerController.health_randomizer.enabled = false -- health randomizer enabled flag
    PlayerController.health_randomizer.bounds_enabled = false -- health randomizer bounds enabled flag
    PlayerController.health_randomizer.lower_bound = 0 -- health randomizer lower bound
    PlayerController.health_randomizer.upper_bound = 100 -- health randomizer upper bound

    -- define the view variables
    PlayerView.health = PlayerParams.Vital_Point -- current health value
    PlayerView.health_changed = false -- slider changed flag

    --[[ 
    Drive parameter initialization 
    ]]
    -- define the controller variables

    PlayerController.drive_randomizer = {}
    PlayerController.drive_randomizer.enabled = false -- drive randomizer enabled flag
    PlayerController.drive_randomizer.bounds_enabled = false -- drive randomizer bounds enabled flag
    PlayerController.drive_randomizer.lower_bound_stock = 0 -- drive randomizer lower bound
    PlayerController.drive_randomizer.upper_bound_stock = 6 -- drive randomizer upper bound
    PlayerController.drive_randomizer.lower_bound_points = 0 -- drive randomizer lower bound
    PlayerController.drive_randomizer.upper_bound_points = 60000 -- drive randomizer upper bound

    -- drive points type, true == absolute, false == percentage
    PlayerController.drive_points_type = false

    -- define the view variables
    PlayerView.burnout = PlayerParams.Is_DG_Break
    PlayerView.burnout_changed = false

    -- drive type, true == stock, false == custom
    PlayerView.drive_type = PlayerParams.DG_Type == 1
    PlayerView.drive_type_changed = false

    -- drive stocks
    PlayerView.drive_stocks = PlayerParams.DG_Stock
    PlayerView.drive_stocks_changed = false

    -- drive points
    PlayerView.drive_points = PlayerParams.DG_Point
    PlayerView.drive_points_changed = false

    --[[
        Super parameter initialization
    ]]
    PlayerController.super_randomizer = {}
    PlayerController.super_randomizer.enabled = false -- super randomizer enabled flag
    PlayerController.super_randomizer.bounds_enabled = false -- super randomizer bounds enabled flag
    PlayerController.super_randomizer.lower_bound_stock = 0 -- super randomizer lower bound
    PlayerController.super_randomizer.upper_bound_stock = 3 -- super randomizer upper bound
    PlayerController.super_randomizer.lower_bound_points = 0 -- super randomizer lower bound
    PlayerController.super_randomizer.upper_bound_points = 30000 -- super randomizer upper bound

    -- super points type
    PlayerController.super_points_type = false

    -- define the view variables

    -- super type, true == stock, false == custom
    PlayerView.super_type = PlayerParams.SA_Type == 1
    PlayerView.super_type_changed = false

    -- super stocks
    PlayerView.super_stocks = PlayerParams.SA_Stock
    PlayerView.super_stocks_changed = false

    -- super points
    PlayerView.super_points = PlayerParams.SA_Point
    PlayerView.super_points_changed = false
end

function PlayerParam:update_player_parameters(PlayerIndex)
    PlayerModel = self.model[PlayerIndex]
    PlayerView = self.view[PlayerIndex]
    PlayerController = self.controller[PlayerIndex]

    local need_apply = false

    -- update health logic
    if not PlayerController.health_randomizer.enabled then
        -- if the randomizer is disabled, use the slider value
        if PlayerView.health_changed then
            PlayerModel.Vital_Point = PlayerView.health
            need_apply = true
        end
    end

    -- update drive logic

    -- drive type
    if PlayerView.drive_type_changed then
        if PlayerView.drive_type then
            -- stock type
            PlayerModel.DG_Type = 1
        else
            -- custom type
            PlayerModel.DG_Type = 3
            PlayerModel.Is_DG_Point_Lock = true
        end
        need_apply = true
    end

    -- burnout
    if PlayerView.burnout_changed then
        PlayerModel.Is_DG_Break = PlayerView.burnout
        need_apply = true
    end

    if not PlayerController.drive_randomizer.enabled then
        -- if the randomizer is disabled, use the slider value

        if PlayerView.drive_points_changed then
            -- custom type
            PlayerModel.DG_Point = PlayerView.drive_points
            PlayerModel.DG_Stock = math.floor((PlayerView.drive_points + 5000) / 10000)
            need_apply = true
        end

        if PlayerView.drive_stocks_changed then
            -- stock type
            PlayerModel.DG_Stock = PlayerView.drive_stocks
            PlayerModel.DG_Point = PlayerView.drive_stocks * 10000
            need_apply = true
        end
    end

    -- update super logic
    if PlayerView.super_type_changed then
        if PlayerView.super_type then
            -- stock type
            PlayerModel.SA_Type = 1
        else
            -- custom type
            PlayerModel.SA_Type = 3
            PlayerModel.Is_SA_Point_Lock = true
        end
        need_apply = true
    end

    if not PlayerController.super_randomizer.enabled then
        -- if the randomizer is disabled, use the slider value

        if PlayerView.super_points_changed then
            -- custom type
            PlayerModel.SA_Point = PlayerView.super_points
            PlayerModel.SA_Stock = math.floor((PlayerView.super_points + 5000) / 10000)
            need_apply = true
        end

        if PlayerView.super_stocks_changed then
            -- stock type
            PlayerModel.SA_Stock = PlayerView.super_stocks
            PlayerModel.SA_Point = PlayerView.super_stocks * 10000
            need_apply = true
        end
    end

    return need_apply
end

function PlayerParam:randomize_player_health(PlayerIndex)
    if not self.controller[PlayerIndex].health_randomizer.enabled then
        return
    end

    PlayerModel = self.model[PlayerIndex]
    PlayerView = self.view[PlayerIndex]
    PlayerController = self.controller[PlayerIndex]

    -- randomize health logic
    local lower_bound = 0
    local upper_bound = 100
    if PlayerController.health_randomizer.bounds_enabled then
        lower_bound = PlayerController.health_randomizer.lower_bound
        upper_bound = PlayerController.health_randomizer.upper_bound
    end
    local randomized_health = math.random(lower_bound, upper_bound)
    PlayerModel.Vital_Point = randomized_health
end

function PlayerParam:randomize_player_drive(PlayerIndex)
    if not self.controller[PlayerIndex].drive_randomizer.enabled then
        return
    end

    PlayerModel = self.model[PlayerIndex]
    PlayerView = self.view[PlayerIndex]
    PlayerController = self.controller[PlayerIndex]

    -- randomize drive logic
    if PlayerView.drive_type then
        -- stock type
        local lower_bound = -6
        local upper_bound = 6
        if PlayerController.drive_randomizer.bounds_enabled then
            lower_bound = PlayerController.drive_randomizer.lower_bound_stock
            upper_bound = PlayerController.drive_randomizer.upper_bound_stock
        end
        local randomized_stocks = math.random(lower_bound, upper_bound)
        if randomized_stocks < 0 then
            randomized_stocks = -randomized_stocks
            PlayerModel.Is_DG_Break = true
        else
            PlayerModel.Is_DG_Break = false
        end
        PlayerModel.DG_Stock = math.max(0, randomized_stocks)
        PlayerModel.DG_Point = PlayerModel.DG_Stock * 10000
    else
        -- point type
        local lower_bound = -60000
        local upper_bound = 60000
        if PlayerController.drive_randomizer.bounds_enabled then
            lower_bound = PlayerController.drive_randomizer.lower_bound_points
            upper_bound = PlayerController.drive_randomizer.upper_bound_points
        end
        local randomized_points = math.random(lower_bound, upper_bound)
        if randomized_points < 0 then
            randomized_points = -randomized_points
            PlayerModel.Is_DG_Break = true
        else
            PlayerModel.Is_DG_Break = false
        end
        PlayerModel.DG_Point = math.max(0, randomized_points)
        PlayerModel.DG_Stock = math.floor((PlayerModel.DG_Point + 5000) / 10000)
    end
end

function PlayerParam:randomize_player_super(PlayerIndex)
    if not self.controller[PlayerIndex].super_randomizer.enabled then
        return
    end

    PlayerModel = self.model[PlayerIndex]
    PlayerView = self.view[PlayerIndex]
    PlayerController = self.controller[PlayerIndex]

    -- randomize super logic
    if PlayerView.super_type then
        -- stock type
        local lower_bound = 0
        local upper_bound = 3
        if PlayerController.super_randomizer.bounds_enabled then
            lower_bound = PlayerController.super_randomizer.lower_bound_stock
            upper_bound = PlayerController.super_randomizer.upper_bound_stock
        end
        local randomized_stocks = math.random(lower_bound, upper_bound)
        PlayerModel.SA_Stock = randomized_stocks
        PlayerModel.SA_Point = PlayerModel.SA_Stock * 10000
    else
        -- point type
        local lower_bound = 0
        local upper_bound = 30000
        if PlayerController.super_randomizer.bounds_enabled then
            lower_bound = PlayerController.super_randomizer.lower_bound_points
            upper_bound = PlayerController.super_randomizer.upper_bound_points
        end
        local randomized_points = math.random(lower_bound, upper_bound)
        PlayerModel.SA_Point = randomized_points
        PlayerModel.SA_Stock = math.floor((PlayerModel.SA_Point + 5000) / 10000)
    end
end

function PlayerParam:draw_health_ui(PlayerIndex)
    PlayerView = self.view[PlayerIndex]
    PlayerController = self.controller[PlayerIndex]
    PlayerModel = self.model[PlayerIndex]
    PlayerLabel = (PlayerIndex == "p1") and "Player 1" or "Player 2"

    -- draw the health UI

    -- health slider
    if PlayerController.health_randomizer.enabled then
        -- disable the slider if the randomizer is enabled
        imgui.begin_disabled()
    end
    PlayerView.health_changed, PlayerView.health =
        imgui.slider_int(PlayerLabel .. " Health Percentage", PlayerModel.Vital_Point, 0, 100)

    if PlayerController.health_randomizer.enabled then
        -- stop the health slider disabled section
        imgui.end_disabled()
    end

    -- randomizer checkbox
    _, PlayerController.health_randomizer.enabled =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Health Randomization", PlayerController.health_randomizer.enabled)

    if PlayerController.health_randomizer.enabled then
        _, PlayerController.health_randomizer.bounds_enabled =
            imgui.checkbox(
            "Enable Bounds for " .. PlayerLabel .. " Health Randomization",
            PlayerController.health_randomizer.bounds_enabled
        )
        if PlayerController.health_randomizer.bounds_enabled then
            -- show the bounds sliders
            _, PlayerController.health_randomizer.lower_bound =
                imgui.drag_int(
                PlayerLabel .. " Health Randomization Lower Bound",
                PlayerController.health_randomizer.lower_bound,
                0.3,
                0,
                PlayerController.health_randomizer.upper_bound
            )
            _, PlayerController.health_randomizer.upper_bound =
                imgui.drag_int(
                PlayerLabel .. " Health Randomization Upper Bound",
                PlayerController.health_randomizer.upper_bound,
                0.3,
                PlayerController.health_randomizer.lower_bound,
                100
            )
        end
    end
end

function PlayerParam:draw_drive_ui(PlayerIndex)
    PlayerView = self.view[PlayerIndex]
    PlayerController = self.controller[PlayerIndex]
    PlayerModel = self.model[PlayerIndex]
    PlayerLabel = (PlayerIndex == "p1") and "Player 1" or "Player 2"

    -- drive slider(s)

    if PlayerController.drive_randomizer.enabled then
        -- disable the slider if the randomizer is enabled
        imgui.begin_disabled()
    end

    -- drive sliders based on type
    if PlayerView.drive_type then
        -- stock type
        PlayerView.drive_stocks_changed, PlayerView.drive_stocks =
            imgui.slider_int(PlayerLabel .. " Drive Stocks", PlayerModel.DG_Stock, 0, 6)
    else
        -- custom type
        if PlayerController.drive_points_type then
            -- absolute type
            PlayerView.drive_points_changed, PlayerView.drive_points =
                imgui.drag_int(PlayerLabel .. " Drive Points", PlayerModel.DG_Point, 1, 0, 60000)
        else
            -- percentage type
            local points_increments = 0
            local current_points = PlayerModel.DG_Point / 10000
            PlayerView.drive_points_changed, points_increments =
                imgui.slider_float(
                PlayerLabel .. " Drive Points (stock increments of 10%)",
                current_points,
                0,
                6,
                "%.1f"
            )
            -- convert to points
            PlayerView.drive_points = math.floor(points_increments * 10000)
        end
    end

    if PlayerController.drive_randomizer.enabled then
        -- stop the drive slider disabled section
        imgui.end_disabled()
    end

    -- stock vs points checkbox
    local type_value = PlayerModel.DG_Type == 1
    PlayerView.drive_type_changed, PlayerView.drive_type =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Drive Type (Stock / Points)", type_value)

    -- on points, show percentage vs absolute toggle
    if not PlayerView.drive_type then
        imgui.same_line()
        _, PlayerController.drive_points_type =
            imgui.checkbox(
            "Toggle " .. PlayerLabel .. " Drive Points Type (Absolute / Percentage)",
            PlayerController.drive_points_type
        )
    end
    -- burnout toggle
    PlayerView.burnout_changed, PlayerView.burnout =
        imgui.checkbox(PlayerLabel .. " Drive Burnout", PlayerModel.Is_DG_Break)

    -- randomizer checkbox
    _, PlayerController.drive_randomizer.enabled =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Drive Randomization", PlayerController.drive_randomizer.enabled)
    -- if randomizer bounds enable checkbox

    if PlayerController.drive_randomizer.enabled then
        -- show the bounds enable checkbox

        _, PlayerController.drive_randomizer.bounds_enabled =
            imgui.checkbox(
            "Enable Bounds for " .. PlayerLabel .. " Drive Randomization",
            PlayerController.drive_randomizer.bounds_enabled
        )

        if PlayerController.drive_randomizer.bounds_enabled then
            -- show the bounds sliders based on type

            if PlayerView.drive_type then
                -- stock type
                _, PlayerController.drive_randomizer.lower_bound_stock =
                    imgui.drag_int(
                    PlayerLabel .. " Drive Stock Randomization Lower Bound",
                    PlayerController.drive_randomizer.lower_bound_stock,
                    0.3,
                    -6,
                    PlayerController.drive_randomizer.upper_bound_stock
                )
                _, PlayerController.drive_randomizer.upper_bound_stock =
                    imgui.drag_int(
                    PlayerLabel .. " Drive Stock Randomization Upper Bound",
                    PlayerController.drive_randomizer.upper_bound_stock,
                    0.3,
                    PlayerController.drive_randomizer.lower_bound_stock,
                    6
                )
            else
                -- point type
                if PlayerController.drive_points_type then
                    -- absolute type
                    _, PlayerController.drive_randomizer.lower_bound_points =
                        imgui.drag_int(
                        PlayerLabel .. " Drive Points Randomization Lower Bound",
                        PlayerController.drive_randomizer.lower_bound_points,
                        1,
                        -60000,
                        PlayerController.drive_randomizer.upper_bound_points
                    )
                    _, PlayerController.drive_randomizer.upper_bound_points =
                        imgui.drag_int(
                        PlayerLabel .. " Drive Points Randomization Upper Bound",
                        PlayerController.drive_randomizer.upper_bound_points,
                        1,
                        PlayerController.drive_randomizer.lower_bound_points,
                        60000
                    )
                else
                    -- percentage type
                    local points_increments_lb = 0
                    local current_points_lb = PlayerController.drive_randomizer.lower_bound_points / 10000

                    local points_increments_ub = 0
                    local current_points_ub = PlayerController.drive_randomizer.upper_bound_points / 10000

                    _, points_increments_lb =
                        imgui.drag_float(
                        PlayerLabel .. " Drive Points Randomization Lower Bound (stock increments of 10%)",
                        current_points_lb,
                        0.1,
                        -6,
                        current_points_ub,
                        "%.1f"
                    )
                    PlayerController.drive_randomizer.lower_bound_points = math.floor(points_increments_lb * 10000)

                    _, points_increments_ub =
                        imgui.drag_float(
                        PlayerLabel .. " Drive Points Randomization Upper Bound (stock increments of 10%)",
                        current_points_ub,
                        0.1,
                        points_increments_lb,
                        6,
                        "%.1f"
                    )
                    PlayerController.drive_randomizer.upper_bound_points = math.floor(points_increments_ub * 10000)
                end
            end
        end
    end
end

function PlayerParam:draw_super_ui(PlayerIndex)
    PlayerView = self.view[PlayerIndex]
    PlayerController = self.controller[PlayerIndex]
    PlayerModel = self.model[PlayerIndex]
    PlayerLabel = (PlayerIndex == "p1") and "Player 1" or "Player 2"

    -- super slider(s)
    if PlayerController.super_randomizer.enabled then
        -- disable the slider if the randomizer is enabled
        imgui.begin_disabled()
    end

    -- super sliders based on type
    if PlayerView.super_type then
        -- stock type
        PlayerView.super_stocks_changed, PlayerView.super_stocks =
            imgui.slider_int(PlayerLabel .. " Super Stocks", PlayerModel.SA_Stock, 0, 3)
    else
        -- custom type
        if PlayerController.super_points_type then
            -- absolute type
            PlayerView.super_points_changed, PlayerView.super_points =
                imgui.drag_int(PlayerLabel .. " Super Points", PlayerModel.SA_Point, 1, 0, 30000)
        else
            -- percentage type
            local points_increments = 0
            local current_points = PlayerModel.SA_Point / 10000
            PlayerView.super_points_changed, points_increments =
                imgui.slider_float(
                PlayerLabel .. " Super Points (stock increments of 10%)",
                current_points,
                0,
                3,
                "%.1f"
            )
            -- convert to points
            PlayerView.super_points = math.floor(points_increments * 10000)
        end
    end

    if PlayerController.super_randomizer.enabled then
        imgui.end_disabled()
    end

    -- stock vs points checkbox
    local type_value = PlayerModel.SA_Type == 1
    PlayerView.super_type_changed, PlayerView.super_type =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Super Type (Stock / Points)", type_value)

    -- on points, show percentage vs absolute toggle
    if not PlayerView.super_type then
        imgui.same_line()
        _, PlayerController.super_points_type =
            imgui.checkbox(
            "Toggle " .. PlayerLabel .. " Super Points Type (Absolute / Percentage)",
            PlayerController.super_points_type
        )
    end

    -- randomizer checkbox
    _, PlayerController.super_randomizer.enabled =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Super Randomization", PlayerController.super_randomizer.enabled)
    -- if randomizer bounds enable checkbox
    if PlayerController.super_randomizer.enabled then
        -- show the bounds enable checkbox

        _, PlayerController.super_randomizer.bounds_enabled =
            imgui.checkbox(
            "Enable Bounds for " .. PlayerLabel .. " Super Randomization",
            PlayerController.super_randomizer.bounds_enabled
        )

        if PlayerController.super_randomizer.bounds_enabled then
            -- show the bounds sliders based on type

            if PlayerView.super_type then
                -- stock type
                _, PlayerController.super_randomizer.lower_bound_stock =
                    imgui.drag_int(
                    PlayerLabel .. " Super Stock Randomization Lower Bound",
                    PlayerController.super_randomizer.lower_bound_stock,
                    0.3,
                    0,
                    PlayerController.super_randomizer.upper_bound_stock
                )
                _, PlayerController.super_randomizer.upper_bound_stock =
                    imgui.drag_int(
                    PlayerLabel .. " Super Stock Randomization Upper Bound",
                    PlayerController.super_randomizer.upper_bound_stock,
                    0.3,
                    PlayerController.super_randomizer.lower_bound_stock,
                    3
                )
            else
                -- point type
                if PlayerController.super_points_type then
                    -- absolute type
                    _, PlayerController.super_randomizer.lower_bound_points =
                        imgui.drag_int(
                        PlayerLabel .. " Super Points Randomization Lower Bound",
                        PlayerController.super_randomizer.lower_bound_points,
                        1,
                        0,
                        PlayerController.super_randomizer.upper_bound_points
                    )
                    _, PlayerController.super_randomizer.upper_bound_points =
                        imgui.drag_int(
                        PlayerLabel .. " Super Points Randomization Upper Bound",
                        PlayerController.super_randomizer.upper_bound_points,
                        1,
                        PlayerController.super_randomizer.lower_bound_points,
                        30000
                    )
                else
                    -- percentage type
                    local points_increments_lb = 0
                    local current_points_lb = PlayerController.super_randomizer.lower_bound_points / 10000

                    local points_increments_ub = 0
                    local current_points_ub = PlayerController.super_randomizer.upper_bound_points / 10000

                    _, points_increments_lb =
                        imgui.drag_float(
                        PlayerLabel .. " Super Points Randomization Lower Bound (stock increments of 10%)",
                        current_points_lb,
                        0.1,
                        0,
                        current_points_ub,
                        "%.1f"
                    )
                    PlayerController.super_randomizer.lower_bound_points = math.floor(points_increments_lb * 10000)
                    _, points_increments_ub =
                        imgui.drag_float(
                        PlayerLabel .. " Super Points Randomization Upper Bound (stock increments of 10%)",
                        current_points_ub,
                        0.1,
                        points_increments_lb,
                        3,
                        "%.1f"
                    )
                    PlayerController.super_randomizer.upper_bound_points = math.floor(points_increments_ub * 10000)
                end
            end
        end
    end
end

function PlayerParam:init(ParameterSettingsData)
    self:init_player("p1", ParameterSettingsData.PlayerDatas[0])
    self:init_player("p2", ParameterSettingsData.PlayerDatas[1])
end

function PlayerParam:draw_ui()
    if imgui.tree_node("Health") then
        if imgui.tree_node("Player 1 Health") then
            self:draw_health_ui("p1")
            imgui.tree_pop()
        end
        if imgui.tree_node("Player 2 Health") then
            self:draw_health_ui("p2")
            imgui.tree_pop()
        end
        imgui.tree_pop()
    end
    if imgui.tree_node("Drive") then
        if imgui.tree_node("Player 1 Drive") then
            self:draw_drive_ui("p1")
            imgui.tree_pop()
        end
        if imgui.tree_node("Player 2 Drive") then
            self:draw_drive_ui("p2")
            imgui.tree_pop()
        end
        imgui.tree_pop()
    end
    if imgui.tree_node("Super") then
        if imgui.tree_node("Player 1 Super") then
            self:draw_super_ui("p1")
            imgui.tree_pop()
        end
        if imgui.tree_node("Player 2 Super") then
            self:draw_super_ui("p2")
            imgui.tree_pop()
        end
        imgui.tree_pop()
    end
end

function PlayerParam:update()
    -- update logic for player parameters

    local need_apply = false

    need_apply = self:update_player_parameters("p1") or need_apply
    need_apply = self:update_player_parameters("p2") or need_apply

    return need_apply
end

function PlayerParam:randomize()
    -- randomization logic for player parameters
    self:randomize_player_health("p1")
    self:randomize_player_health("p2")
    self:randomize_player_drive("p1")
    self:randomize_player_drive("p2")
    self:randomize_player_super("p1")
    self:randomize_player_super("p2")
end

--[[
    Module level logic
]]
function module.init()
    -- load the game parameters
    module.data.TrainingManager = sdk.get_managed_singleton("app.training.TrainingManager")
    module.data.TrainingData = module.data.TrainingManager:get_field("_tData")
    module.data.ParameterSetting = module.data.TrainingData:get_field("ParameterSetting")
    module.data.tf_PS = module.data.TrainingManager._tfFuncs._entries[6]:get_field("value")
    local gBattle = sdk.find_type_definition("gBattle")
    module.data.sGame = gBattle:get_field("Game"):get_data(nil)

    -- initialize player parameters
    PlayerParam:init(module.data.ParameterSetting)
end

function module.on_frame()
    -- module logic goes here
    local need_apply = false

    need_apply = PlayerParam:update() or need_apply

    -- randomization logic
    -- for now just use this, later set this to a bind or something
    request_randomizer = module.data.TrainingManager._IsReqRefresh
    if request_randomizer then
        -- randomize parameters
        PlayerParam:randomize()
    end

    -- apply the settings if needed
    if need_apply then
        sdk.call_object_func(module.data.tf_PS, "bApply")
    end
end

function module.draw_ui()
    -- module level UI
    if imgui.collapsing_header("Training Parameters") then
        -- player specific UI
        PlayerParam:draw_ui()
    end
end

return module
