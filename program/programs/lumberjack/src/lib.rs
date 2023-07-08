use anchor_lang::prelude::*;
use gpl_session::{SessionError, SessionToken, session_auth_or, Session};

declare_id!("9eSWUTsPxc3HEVCKe1oHBo7fXPSua5dHZkN48k2Q8yyL");

#[error_code]
pub enum GameErrorCode {
    #[msg("Not enough energy")]
    NotEnoughEnergy,
    #[msg("Not enough gold")]
    NotEnoughGold,
    #[msg("Not ready for harvest yet")]
    NotReadyYet,
    #[msg("Nothing was planted")]
    NothingWasPlanted,
    #[msg("Wrong Authority")]
    WrongAuthority,
}

const MAX_ENERGY: u64 = 10;

#[program]
pub mod lumberjack {
    use super::*;

    pub fn init_player(ctx: Context<InitPlayer>) -> Result<()> {
        ctx.accounts.player.energy = MAX_ENERGY;
        ctx.accounts.player.last_login = Clock::get()?.unix_timestamp;
        ctx.accounts.player.gold = 5;
        ctx.accounts.player.authority = ctx.accounts.signer.key();
        Ok(())
    }

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn plant( ctx: Context<Plant>, human_type: String) -> Result<()> {

        if (ctx.accounts.plot.human_type != "") {
            return err!(GameErrorCode::NotReadyYet);
        }

        let cost: u64 = match human_type.as_str() {
            "peasant" => 5,
            "breadmaker" => 10,
            "beerbrewer" => 30,
            "blacksmith" => 50,
            "solanadev" => 250,
            _ => return err!(GameErrorCode::NotEnoughGold),
        };

        if ctx.accounts.player.gold < cost {
            return err!(GameErrorCode::NotEnoughGold);
        }
        ctx.accounts.plot.human_type = human_type;
        ctx.accounts.plot.planted_at = Clock::get()?.unix_timestamp;
        ctx.accounts.player.gold -= cost;
        msg!("You planted a human {} for {} gold.", ctx.accounts.plot.human_type, cost);
        Ok(())
    }

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn harvest( ctx: Context<Harvest>) -> Result<()> {

        let reward: u64 = match ctx.accounts.plot.human_type.as_str() {
            "peasant" => 10,
            "breadmaker" => 20,
            "beerbrewer" => 60,
            "blacksmith" => 100,
            "solanadev" => 500,
            _ => return err!(GameErrorCode::NotEnoughGold),
        };

        if (ctx.accounts.plot.planted_at + 20) > Clock::get()?.unix_timestamp {
            return err!(GameErrorCode::NotReadyYet);
        }
        if ctx.accounts.plot.human_type == "" {
            return err!(GameErrorCode::NothingWasPlanted);
        }

        ctx.accounts.plot.human_type = "".to_string();
        ctx.accounts.player.gold += reward;
        msg!("You harvested a {} for {} gold.", ctx.accounts.plot.human_type.as_str(), reward);
        Ok(())
    }

    pub fn update( _ctx: Context<ChopTree>) -> Result<()> {
        // nothing to do here currently
        //msg!("Updated energy. You have {} wood and {} energy left.", ctx.accounts.player.wood, ctx.accounts.player.energy);
        Ok(())
    }
}

#[derive(Accounts)]
pub struct InitPlayer <'info> {
    #[account( 
        init,
        payer = signer,
        space = 1000,
        seeds = [b"player".as_ref(), signer.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account( 
        init,
        payer = signer,
        space = 1000,
        seeds = [b"plot".as_ref(), signer.key().as_ref()],
        bump,
    )]
    pub plot: Account<'info, Plot>,
    #[account(mut)]
    pub signer: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[account]
pub struct PlayerData {
    pub authority: Pubkey,
    pub name: String,
    pub level: u8,
    pub xp: u64,
    pub energy: u64,
    pub gold: u64,
    pub last_login: i64
}

#[derive(Accounts, Session)]
pub struct ChopTree <'info> {
    #[session(
        // The ephemeral key pair signing the transaction
        signer = signer,
        // The authority of the user account which must have created the session
        authority = player.authority.key()
    )]
    // Session Tokens are passed as optional accounts
    pub session_token: Option<Account<'info, SessionToken>>,

    #[account( 
        mut,
        seeds = [b"player".as_ref(), player.authority.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account(mut)]
    pub signer: Signer<'info>,
}

#[derive(Accounts, Session)]
pub struct Plant <'info> {
    #[session(
        // The ephemeral key pair signing the transaction
        signer = signer,
        // The authority of the user account which must have created the session
        authority = player.authority.key()
    )]
    // Session Tokens are passed as optional accounts
    pub session_token: Option<Account<'info, SessionToken>>,

    #[account( 
        mut,
        seeds = [b"player".as_ref(), player.authority.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account( 
        mut,
        seeds = [b"plot".as_ref(), player.authority.key().as_ref()],
        bump,
    )]    
    pub plot: Account<'info, Plot>,
    #[account(mut)]
    pub signer: Signer<'info>,
}

#[derive(Accounts, Session)]
pub struct Harvest <'info> {
    #[session(
        // The ephemeral key pair signing the transaction
        signer = signer,
        // The authority of the user account which must have created the session
        authority = player.authority.key()
    )]
    // Session Tokens are passed as optional accounts
    pub session_token: Option<Account<'info, SessionToken>>,

    #[account( 
        mut,
        seeds = [b"player".as_ref(), player.authority.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account( 
        mut,
        seeds = [b"plot".as_ref(), player.authority.key().as_ref()],
        bump,
    )]    
    pub plot: Account<'info, Plot>,
    #[account(mut)]
    pub signer: Signer<'info>,
}


#[account]
pub struct Plot {
    pub human_type: String,
    pub planted_at: i64
}